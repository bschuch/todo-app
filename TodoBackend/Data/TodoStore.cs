using TodoBackend.Models;

namespace TodoBackend.Data;

public static class TodoStore
{
    public static List<Todo> Todos { get; } = new List<Todo>
    {
        new() { Id = 1, Title = "Setup git repo", Completed = false },
        new() { Id = 2, Title = "Build GraphQl API", Completed = true },
        // new Todo { Id = 2, Title = "Walk the dog", Completed = true },
        new() { Id = 3, Title = "Connect React API", Completed = false }
    };
}