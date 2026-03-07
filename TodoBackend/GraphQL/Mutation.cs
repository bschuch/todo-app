using TodoBackend.Data;
using TodoBackend.Models;

namespace TodoBackend.GraphQL;

public class Mutation
{
    public Todo AddTodo(string title)
    {
        var newTodo = new Todo
        {
            Id = TodoStore.Todos.Count > 0 ? TodoStore.Todos.Max(t => t.Id) + 1 : 1,
            Title = title,
            Completed = false
        };
        TodoStore.Todos.Add(newTodo);
        return newTodo;
    }
}