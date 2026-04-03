using Microsoft.EntityFrameworkCore;
using TodoBackend.Data;
using TodoBackend.Models;
using MongoDB.Bson;

namespace TodoBackend.GraphQL;

public class Mutation
{
    // delete todo by id
    public async Task<bool> DeleteTodo(string id, [Service] TodoDbContext dbContext)
    {
        // Find the todo item by id
        var todo = await dbContext.Todos.FirstOrDefaultAsync(t => t.Id == id);
        if (todo == null) return false; // Return false if not found

        dbContext.Todos.Remove(todo);
        await dbContext.SaveChangesAsync();
        return true;
    }
    public Todo AddTodo(string title, [Service] TodoDbContext context)
    {
        var newTodo = new Todo
        {
            // Id = TodoStore.Todos.Count > 0 ? TodoStore.Todos.Max(t => t.Id) + 1 : 1,
            // Id = System.Guid.NewGuid().ToString(),
            Id = ObjectId.GenerateNewId().ToString(),
            Title = title,
            IsCompleted = false
        };
        context.Todos.Add(newTodo);
        context.SaveChangesAsync();// vs. SaveChanges() - async is recommended for real applications to avoid blocking threads, but for simplicity in this example we can use the synchronous version
        return newTodo;
    }

    public async Task<Todo?> ToggleTodoCompletion(string id, [Service] TodoDbContext dbContext)
    {
        // var todo = await dbContext.Todos.FindAsync(id);
        var todo = await dbContext.Todos.FirstOrDefaultAsync(t => t.Id == id);
        if (todo == null) return null;

        // Flip the completion status
        todo.IsCompleted = !todo.IsCompleted;
        await dbContext.SaveChangesAsync();
        return todo;
    }
}