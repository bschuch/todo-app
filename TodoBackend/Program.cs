using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
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

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173", "http://localhost:5174"];

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.WithOrigins(allowedOrigins)
              .SetIsOriginAllowed(origin => builder.Environment.IsDevelopment() && IsAllowedFrontendOrigin(origin))
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var mongoConnectionString = builder.Configuration["Mongo:ConnectionString"] ?? "mongodb://localhost:27017";
var mongoDatabaseName = builder.Configuration["Mongo:DatabaseName"] ?? "TodoDatabase";
var mongoClient = new MongoDB.Driver.MongoClient(mongoConnectionString);
builder.Services.AddDbContext<TodoDbContext>(options =>
    options.UseMongoDB(mongoClient, mongoDatabaseName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthService>();

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = builder.Environment.IsDevelopment());

var app = builder.Build();

app.UseCors("FrontendPolicy");

// 3. Add GraphQL endpoint
app.MapGraphQL();

await SeedDataAsync(app.Services);

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
            SessionToken = AuthService.CreateSessionToken(),
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
        Title = title,
        StartAt = startAt,
        EndAt = startAt.AddMinutes(durationMinutes),
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
