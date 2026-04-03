using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.EntityFrameworkCore.Extensions;
using MongoDB.EntityFrameworkCore.ValueGeneration;
using TodoBackend.Models;

namespace TodoBackend.Data;

public class TodoDbContext : DbContext
{
    public DbSet<Todo> Todos { get; set; } = null!;

    public TodoDbContext(DbContextOptions<TodoDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Map to specefic MongoDb Collection
        modelBuilder.Entity<Todo>().ToCollection("todos");
        modelBuilder.Entity<Todo>()
            .HasKey(t => t.Id);

        modelBuilder.Entity<Todo>()
            .Property(t => t.Id)
            .HasConversion<ObjectId>()
            .HasValueGenerator<ObjectIdValueGenerator>();
    }
}