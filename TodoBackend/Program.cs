using System.Diagnostics;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;
using HotChocolate.AspNetCore;
using TodoBackend.Data;
using TodoBackend.GraphQL;
using TodoBackend.Models;
using TodoBackend.Services;
using WorkflowTaskStatus = TodoBackend.Models.TaskStatus;
var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

var isHosted = !builder.Environment.IsDevelopment();
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

var mongoConnectionString = builder.Configuration["Mongo:ConnectionString"];
var mongoDatabaseName = builder.Configuration["Mongo:DatabaseName"];
if (builder.Environment.IsDevelopment())
{
    mongoConnectionString ??= "mongodb://localhost:27017";
    mongoDatabaseName ??= "TodoDatabase";
}

if (isHosted && (string.IsNullOrWhiteSpace(mongoConnectionString) ||
                 string.IsNullOrWhiteSpace(mongoDatabaseName) ||
                 allowedOrigins.Length == 0))
{
    throw new InvalidOperationException("Hosted environments require Mongo:ConnectionString, Mongo:DatabaseName, and at least one Cors:AllowedOrigins value.");
}

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.WithOrigins(allowedOrigins)
              .SetIsOriginAllowed(origin => builder.Environment.IsDevelopment() && IsAllowedFrontendOrigin(origin))
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var mongoClient = new MongoClient(mongoConnectionString!);
var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseName!);
builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(mongoDatabase);
builder.Services.AddDbContext<TodoDbContext>(options =>
    options.UseMongoDB(mongoClient, mongoDatabaseName!));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<AuthAttemptLimiter>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("graphql", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(10, builder.Configuration.GetValue("RateLimit:GraphQLRequestsPerMinute", 120)),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = builder.Environment.IsDevelopment());

var app = builder.Build();

app.UseForwardedHeaders();
if (isHosted)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        await next();
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Unhandled request failure for {Method} {Path}", context.Request.Method, context.Request.Path);
        throw;
    }
    finally
    {
        app.Logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds}ms",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds);
    }
});

app.UseCors("FrontendPolicy");
app.UseRateLimiter();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (IMongoDatabase database, CancellationToken cancellationToken) =>
{
    try
    {
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
        return Results.Ok(new { status = "ready" });
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapGraphQL()
    .WithOptions(new GraphQLServerOptions
    {
        Tool = { Enable = builder.Environment.IsDevelopment() },
        EnableSchemaRequests = builder.Environment.IsDevelopment()
    })
    .RequireRateLimiting("graphql");

await EnsureIndexesAsync(mongoDatabase);
if (builder.Configuration.GetValue("SeedDemoData", false))
{
    await SeedDataAsync(app.Services);
}

app.Run();

static async Task SeedDataAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<TodoDbContext>();

    var now = DateTime.UtcNow;
    var defaultFamily = await dbContext.Families.FirstOrDefaultAsync(family => family.BoardId == "family-home");
    if (defaultFamily == null)
    {
        defaultFamily = new Family
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Smith Family",
            BoardId = "family-home",
            Color = "#3479b5",
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Families.Add(defaultFamily);
        await dbContext.SaveChangesAsync();
    }

    var demoUser = await dbContext.AppUsers.FirstOrDefaultAsync(user => user.Email == "demo@family.local");
    if (demoUser == null)
    {
        demoUser = new AppUser
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Email = "demo@family.local",
            DisplayName = "Demo Family",
            PasswordHash = AuthService.HashPassword("family-demo"),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.AppUsers.Add(demoUser);
        await dbContext.SaveChangesAsync();
    }

    if (!await dbContext.FamilyMemberships.AnyAsync(membership => membership.UserId == demoUser.Id && membership.FamilyId == defaultFamily.Id))
    {
        dbContext.FamilyMemberships.Add(new FamilyMembership
        {
            Id = ObjectId.GenerateNewId().ToString(),
            FamilyId = defaultFamily.Id,
            UserId = demoUser.Id,
            Role = "Owner",
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync();
    }

    if (!await dbContext.FamilyMembers.AnyAsync(member => member.FamilyId == defaultFamily.Id))
    {
        dbContext.FamilyMembers.AddRange(
            new FamilyMember { Id = ObjectId.GenerateNewId().ToString(), FamilyId = defaultFamily.Id, Name = "Dad", Color = "#6dbec2", CreatedAt = now, UpdatedAt = now },
            new FamilyMember { Id = ObjectId.GenerateNewId().ToString(), FamilyId = defaultFamily.Id, Name = "Ella", Color = "#f2a7b2", CreatedAt = now, UpdatedAt = now },
            new FamilyMember { Id = ObjectId.GenerateNewId().ToString(), FamilyId = defaultFamily.Id, Name = "Harper", Color = "#b89acb", CreatedAt = now, UpdatedAt = now },
            new FamilyMember { Id = ObjectId.GenerateNewId().ToString(), FamilyId = defaultFamily.Id, Name = "Mom", Color = "#d67268", CreatedAt = now, UpdatedAt = now },
            new FamilyMember { Id = ObjectId.GenerateNewId().ToString(), FamilyId = defaultFamily.Id, Name = "Liam", Color = "#a7d6a1", CreatedAt = now, UpdatedAt = now }
        );
        await dbContext.SaveChangesAsync();
    }

    var seededMembers = await dbContext.FamilyMembers
        .Where(member => member.FamilyId == defaultFamily.Id)
        .ToListAsync();
    var dad = seededMembers.FirstOrDefault(member => member.Name == "Dad");
    var ella = seededMembers.FirstOrDefault(member => member.Name == "Ella");
    var harper = seededMembers.FirstOrDefault(member => member.Name == "Harper");
    var mom = seededMembers.FirstOrDefault(member => member.Name == "Mom");
    var liam = seededMembers.FirstOrDefault(member => member.Name == "Liam");

    if (!await dbContext.CalendarEvents.AnyAsync(calendarEvent => calendarEvent.FamilyId == defaultFamily.Id))
    {
        var today = DateTime.UtcNow.Date;
        var startOfWeek = today.AddDays(-(int)today.DayOfWeek);

        dbContext.CalendarEvents.AddRange(
            CreateSeedEvent(defaultFamily.Id, dad?.Id, "Grocery Run", startOfWeek.AddDays(0).AddHours(15), 90, dad?.Color ?? "#6dbec2"),
            CreateSeedEvent(defaultFamily.Id, mom?.Id, "Amelia's Baby Shower", startOfWeek.AddDays(0).AddHours(19), 180, mom?.Color ?? "#d67268"),
            CreateSeedEvent(defaultFamily.Id, liam?.Id, "Dog's Big Bath Day!", startOfWeek.AddDays(1).AddHours(14).AddMinutes(15), 60, liam?.Color ?? "#a7d6a1"),
            CreateSeedEvent(defaultFamily.Id, mom?.Id, "Coffee With Diane", startOfWeek.AddDays(1).AddHours(16).AddMinutes(30), 90, mom?.Color ?? "#d67268"),
            CreateSeedEvent(defaultFamily.Id, harper?.Id, "Tutoring", startOfWeek.AddDays(1).AddHours(20), 60, harper?.Color ?? "#b89acb"),
            CreateSeedEvent(defaultFamily.Id, dad?.Id, "Drop Off Dry Cleaning", startOfWeek.AddDays(2).AddHours(13).AddMinutes(30), 60, dad?.Color ?? "#6dbec2"),
            CreateSeedEvent(defaultFamily.Id, ella?.Id, "History Test", startOfWeek.AddDays(2).AddHours(15).AddMinutes(30), 60, ella?.Color ?? "#f2a7b2"),
            CreateSeedEvent(defaultFamily.Id, dad?.Id, "Pest Control", startOfWeek.AddDays(2).AddHours(17).AddMinutes(15), 120, dad?.Color ?? "#6dbec2"),
            CreateSeedEvent(defaultFamily.Id, harper?.Id, "Pep Rally!", startOfWeek.AddDays(3).AddHours(16), 90, harper?.Color ?? "#b89acb"),
            CreateSeedEvent(defaultFamily.Id, ella?.Id, "Pajama Day!", startOfWeek.AddDays(4).AddHours(14), 60, ella?.Color ?? "#f2a7b2")
        );
        await dbContext.SaveChangesAsync();
    }

    if (await dbContext.Todos.AnyAsync(task => task.BoardId == defaultFamily.BoardId))
    {
        return;
    }

    var seedTasks = new[]
    {
        new Todo { Title = "Clean room", AssigneeName = "Emma", Status = WorkflowTaskStatus.Todo, SortOrder = 0, BoardId = "family-home", Completed = false },
        new Todo { Title = "Homework review", AssigneeName = "Emma", Status = WorkflowTaskStatus.InProgress, SortOrder = 0, BoardId = "family-home", Completed = false },
        new Todo { Title = "Replace air filters", AssigneeName = "James", Status = WorkflowTaskStatus.Todo, SortOrder = 1, BoardId = "family-home", Completed = false },
        new Todo { Title = "Prep DMV paperwork", AssigneeName = "James", Status = WorkflowTaskStatus.Done, SortOrder = 0, BoardId = "family-home", Completed = true },
        new Todo { Title = "Unload dishwasher", AssigneeName = "Katie", Status = WorkflowTaskStatus.InProgress, SortOrder = 1, BoardId = "family-home", Completed = false },
        new Todo { Title = "Call vet", AssigneeName = "Marcus", Status = WorkflowTaskStatus.Todo, SortOrder = 2, BoardId = "family-home", Completed = false }
    };

    foreach (var task in seedTasks)
    {
        task.Id = ObjectId.GenerateNewId().ToString();
        task.CreatedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
    }

    dbContext.Todos.AddRange(seedTasks);
    await dbContext.SaveChangesAsync();
}

static CalendarEvent CreateSeedEvent(string familyId, string? memberId, string title, DateTime startAt, int durationMinutes, string tone) =>
    new()
    {
        Id = ObjectId.GenerateNewId().ToString(),
        FamilyId = familyId,
        MemberId = memberId,
        MemberIds = string.IsNullOrWhiteSpace(memberId) ? new List<string>() : new List<string> { memberId },
        Title = title,
        StartAt = startAt,
        EndAt = startAt.AddMinutes(durationMinutes),
        IsAllDay = false,
        Tone = tone,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

static bool IsAllowedFrontendOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    if (uri.Scheme != Uri.UriSchemeHttp)
    {
        return false;
    }

    if (uri.Port is not (5173 or 5174))
    {
        return false;
    }

    if (uri.Host is "localhost" or "127.0.0.1")
    {
        return true;
    }

    return uri.Host.StartsWith("192.168.", StringComparison.Ordinal) ||
           uri.Host.StartsWith("10.", StringComparison.Ordinal) ||
           IsPrivate172Address(uri.Host);
}

static bool IsPrivate172Address(string host)
{
    var parts = host.Split('.');
    return parts.Length == 4 &&
           parts[0] == "172" &&
           int.TryParse(parts[1], out var secondOctet) &&
           secondOctet is >= 16 and <= 31;
}

static async Task EnsureIndexesAsync(IMongoDatabase database)
{
    static CreateIndexModel<BsonDocument> Index(BsonDocument keys, bool unique = false, TimeSpan? expireAfter = null) =>
        new(keys, new CreateIndexOptions { Unique = unique, ExpireAfter = expireAfter });

    await database.GetCollection<BsonDocument>("appUsers").Indexes.CreateOneAsync(Index(new BsonDocument("Email", 1), unique: true));
    await database.GetCollection<BsonDocument>("familyInvites").Indexes.CreateOneAsync(Index(new BsonDocument("Code", 1), unique: true));
    await database.GetCollection<BsonDocument>("appSessions").Indexes.CreateManyAsync([
        Index(new BsonDocument("TokenHash", 1), unique: true),
        Index(new BsonDocument("ExpiresAt", 1), expireAfter: TimeSpan.Zero)
    ]);
    await database.GetCollection<BsonDocument>("familyMemberships").Indexes.CreateOneAsync(
        Index(new BsonDocument { { "FamilyId", 1 }, { "UserId", 1 } }, unique: true));
    await database.GetCollection<BsonDocument>("calendarEvents").Indexes.CreateOneAsync(
        Index(new BsonDocument { { "FamilyId", 1 }, { "StartAt", 1 }, { "EndAt", 1 } }));
    await database.GetCollection<BsonDocument>("todos").Indexes.CreateOneAsync(
        Index(new BsonDocument { { "FamilyId", 1 }, { "DueAt", 1 } }));
}
