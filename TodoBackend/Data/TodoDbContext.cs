using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.EntityFrameworkCore.Extensions;
using MongoDB.EntityFrameworkCore.ValueGeneration;
using TodoBackend.Models;

namespace TodoBackend.Data;

public class TodoDbContext : DbContext
{
    public DbSet<Todo> Todos { get; set; } = null!;
    public DbSet<Family> Families { get; set; } = null!;
    public DbSet<FamilyMember> FamilyMembers { get; set; } = null!;
    public DbSet<CalendarEvent> CalendarEvents { get; set; } = null!;

    public TodoDbContext(DbContextOptions<TodoDbContext> options) : base(options)
    {
        Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureMongoEntity<Todo>(modelBuilder, "todos");
        ConfigureMongoEntity<Family>(modelBuilder, "families");
        ConfigureMongoEntity<FamilyMember>(modelBuilder, "familyMembers");
        ConfigureMongoEntity<CalendarEvent>(modelBuilder, "calendarEvents");
    }

    private static void ConfigureMongoEntity<TEntity>(ModelBuilder modelBuilder, string collectionName)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>().ToCollection(collectionName);
        modelBuilder.Entity<TEntity>().HasKey("Id");
        modelBuilder.Entity<TEntity>()
            .Property<string>("Id")
            .HasConversion<ObjectId>()
            .HasValueGenerator<ObjectIdValueGenerator>();
    }
}
