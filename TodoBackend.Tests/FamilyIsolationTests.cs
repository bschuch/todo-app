using HotChocolate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using MongoDB.Bson;
using MongoDB.Driver;
using TodoBackend.Data;
using TodoBackend.GraphQL;
using TodoBackend.Models;
using TodoBackend.Services;
using Xunit;

namespace TodoBackend.Tests;

public sealed class MongoFactAttribute : FactAttribute
{
    public MongoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_MONGO_CONNECTION_STRING")))
            Skip = "Set TEST_MONGO_CONNECTION_STRING to run isolated MongoDB integration tests.";
    }
}

public class FamilyIsolationTests : IAsyncLifetime
{
    private MongoClient? client;
    private TodoDbContext db = null!;
    private AuthService auth = null!;
    private readonly string databaseName = $"family_isolation_tests_{Guid.NewGuid():N}";
    private readonly HttpContextAccessor accessor = new() { HttpContext = new DefaultHttpContext() };
    private readonly Family first = new() { Id = Id(), Name = "Smith", BoardId = "smith" };
    private readonly Family second = new() { Id = Id(), Name = "Smith", BoardId = "smith" };
    private string userId = "";
    private Todo own = null!;
    private Todo other = null!;
    private Todo orphan = null!;
    private Todo invalid = null!;
    private static string Id() => ObjectId.GenerateNewId().ToString();

    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("TEST_MONGO_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connection)) return;
        client = new MongoClient(connection);
        db = new TodoDbContext(new DbContextOptionsBuilder<TodoDbContext>().UseMongoDB(client, databaseName).Options);
        auth = new AuthService(accessor, new TestEnvironment(), new ConfigurationBuilder().Build());
        userId = Id();
        db.AppUsers.Add(new AppUser { Id = userId, Email = "first@example.test", DisplayName = "First", PasswordHash = "unused" });
        db.Families.AddRange(first, second);
        db.FamilyMemberships.Add(new FamilyMembership { Id = Id(), FamilyId = first.Id, UserId = userId, Role = "Owner" });
        own = new Todo { Id = Id(), Title = "Own", FamilyId = first.Id, BoardId = "smith" };
        other = new Todo { Id = Id(), Title = "Other", FamilyId = second.Id, BoardId = "smith", SortOrder = 7 };
        orphan = new Todo { Id = Id(), Title = "Orphan", BoardId = "unmatched" };
        invalid = new Todo { Id = Id(), Title = "Invalid", FamilyId = Id(), BoardId = "smith" };
        db.Todos.AddRange(own, other, orphan, invalid);
        await db.SaveChangesAsync();
        accessor.HttpContext!.Request.Headers.Authorization = $"Bearer {await auth.CreateSessionAsync(db, userId)}";
    }

    public async Task DisposeAsync()
    {
        if (client == null) return;
        await db.DisposeAsync();
        await client.DropDatabaseAsync(databaseName);
    }

    [MongoFact]
    public async Task QueriesExcludeOtherFamiliesAndOrphansEvenWithIdenticalBoards()
    {
        var query = new Query();
        Assert.Equal(own.Id, Assert.Single(await query.GetTodos(db, auth)).Id);
        Assert.Equal(own.Id, Assert.Single(await query.GetTasks(db, auth, first.Id)).Id);
        await Assert.ThrowsAsync<GraphQLException>(() => query.GetTasks(db, auth, second.Id));
        accessor.HttpContext!.Request.Headers.Authorization = "";
        await Assert.ThrowsAsync<GraphQLException>(() => query.GetTodos(db, auth));
        await Assert.ThrowsAsync<GraphQLException>(() => query.GetTasks(db, auth, first.Id));
    }

    [MongoFact]
    public async Task AllTaskMutationsDenyForeignAndInvalidOwnership()
    {
        var mutation = new Mutation();
        foreach (var task in new[] { other, orphan, invalid })
        {
            await Assert.ThrowsAsync<GraphQLException>(() => mutation.ToggleTaskCompletion(task.Id, db, auth));
            await Assert.ThrowsAsync<GraphQLException>(() => mutation.MoveTask(task.Id, TodoBackend.Models.TaskStatus.Done, 0, db, auth));
            await Assert.ThrowsAsync<GraphQLException>(() => mutation.UpdateTaskSchedule(task.Id, null, null, null, db, auth));
            await Assert.ThrowsAsync<GraphQLException>(() => mutation.DeleteTask(task.Id, db, auth));
        }
        await Assert.ThrowsAsync<GraphQLException>(() => mutation.CreateTask("Denied", "", db, auth, second.Id));
        await Assert.ThrowsAsync<GraphQLException>(() => mutation.CreateTask("Denied", "", db, auth, ""));
        Assert.Equal(4, await db.Todos.CountAsync());
    }

    [MongoFact]
    public async Task LegitimateWritesAndFamilyDeletionDoNotAffectCollidingFamily()
    {
        var mutation = new Mutation();
        var created = await mutation.CreateTask("New", "", db, auth, first.Id);
        Assert.Equal(first.Id, created.FamilyId);
        Assert.Equal(first.BoardId, created.BoardId);
        Assert.Equal(1, created.SortOrder);
        await mutation.MoveTask(created.Id, TodoBackend.Models.TaskStatus.Todo, 0, db, auth);
        await mutation.ToggleTaskCompletion(own.Id, db, auth);
        await mutation.UpdateTaskSchedule(created.Id, DateTime.UtcNow, 30, null, db, auth);
        await mutation.DeleteTask(own.Id, db, auth);
        Assert.True(await mutation.DeleteFamily(first.Id, db, auth));
        db.ChangeTracker.Clear();
        var untouched = await db.Todos.SingleAsync(task => task.Id == other.Id);
        Assert.Equal(7, untouched.SortOrder);
        Assert.False(untouched.Completed);
        Assert.True(await db.Families.AnyAsync(family => family.Id == second.Id));
    }

    [MongoFact]
    public async Task RemovedMembersAndUsersWithoutMembershipCannotAccessTasks()
    {
        db.FamilyMemberships.RemoveRange(await db.FamilyMemberships.ToListAsync());
        await db.SaveChangesAsync();
        Assert.Empty(await new Query().GetTodos(db, auth));
        await Assert.ThrowsAsync<GraphQLException>(() => new Query().GetTasks(db, auth, first.Id));
        await Assert.ThrowsAsync<GraphQLException>(() => new Mutation().DeleteTask(own.Id, db, auth));
    }

    [MongoFact]
    public async Task ExpiredAndRevokedSessionsCannotReadTasks()
    {
        var session = await db.AppSessions.SingleAsync();
        session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<GraphQLException>(() => new Query().GetTasks(db, auth, first.Id));
        accessor.HttpContext!.Request.Headers.Authorization = $"Bearer {await auth.CreateSessionAsync(db, userId)}";
        Assert.True(await auth.RevokeCurrentSessionAsync(db));
        await Assert.ThrowsAsync<GraphQLException>(() => new Query().GetTasks(db, auth, first.Id));
    }

    [MongoFact]
    public async Task MigrationDryRunDoesNotWriteAndApplyIsIdempotent()
    {
        var legacy = new Todo { Id = Id(), Title = "Legacy", BoardId = "unique" };
        first.BoardId = "unique";
        db.Todos.Add(legacy);
        await db.SaveChangesAsync();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        await TaskOwnershipMigration.RunAsync(db, logger, false);
        db.ChangeTracker.Clear();
        Assert.Null((await db.Todos.SingleAsync(task => task.Id == legacy.Id)).FamilyId);
        await TaskOwnershipMigration.RunAsync(db, logger, true);
        db.ChangeTracker.Clear();
        Assert.Equal(first.Id, (await db.Todos.SingleAsync(task => task.Id == legacy.Id)).FamilyId);
        await TaskOwnershipMigration.RunAsync(db, logger, true);
        db.ChangeTracker.Clear();
        Assert.Equal(first.Id, (await db.Todos.SingleAsync(task => task.Id == legacy.Id)).FamilyId);
        Assert.Null((await db.Todos.SingleAsync(task => task.Id == orphan.Id)).FamilyId);
        Assert.Equal(second.Id, (await db.Todos.SingleAsync(task => task.Id == other.Id)).FamilyId);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
