using TodoBackend.Data;
using TodoBackend.Models;
namespace TodoBackend.GraphQL;

public class Query
{
    // private static readonly List<Todo> _todos = new List<Todo>
    // {
    //     new Todo { Id = 1, Title = "Buy groceries", Completed = false },
    //     new Todo { Id = 2, Title = "Walk the dog", Completed = true },
    //     new Todo { Id = 3, Title = "Finish homework", Completed = false }
    // };

    // public IEnumerable<Todo> GetTodos() => TodoStore.Todos;
    public IQueryable<Todo> GetTodos([Service] TodoDbContext context) => context.Todos;
}