using TodoBackend.Data;
using TodoBackend.Models;
using Microsoft.EntityFrameworkCore;
namespace TodoBackend.GraphQL;

public class Query
{
    public IQueryable<Family> GetFamilies([Service] TodoDbContext context) =>
        context.Families.OrderBy(family => family.Name);

    public async Task<Family?> GetFamily([Service] TodoDbContext context, string id) =>
        await context.Families.FirstOrDefaultAsync(family => family.Id == id);

    public IQueryable<FamilyMember> GetFamilyMembers([Service] TodoDbContext context, string familyId) =>
        context.FamilyMembers
            .Where(member => member.FamilyId == familyId)
            .OrderBy(member => member.Name);

    public IQueryable<CalendarEvent> GetCalendarEvents(
        [Service] TodoDbContext context,
        string familyId,
        DateTime rangeStart,
        DateTime rangeEnd) =>
        context.CalendarEvents
            .Where(calendarEvent =>
                calendarEvent.FamilyId == familyId &&
                calendarEvent.StartAt < rangeEnd &&
                calendarEvent.EndAt > rangeStart)
            .OrderBy(calendarEvent => calendarEvent.StartAt);

    public IQueryable<Todo> GetTasks(
        [Service] TodoDbContext context,
        string boardId = "family-home",
        bool includeCompleted = true)
    {
        var query = context.Todos.Where(task => task.BoardId == boardId);

        if (!includeCompleted)
        {
            query = query.Where(task => !task.Completed);
        }

        return query.OrderBy(task => task.Status).ThenBy(task => task.SortOrder);
    }

    public IQueryable<Todo> GetScheduledTasks(
        [Service] TodoDbContext context,
        string familyId,
        DateTime rangeStart,
        DateTime rangeEnd) =>
        context.Todos
            .Where(task =>
                task.FamilyId == familyId &&
                task.DueAt != null &&
                task.DueAt >= rangeStart &&
                task.DueAt < rangeEnd)
            .OrderBy(task => task.DueAt);

    public IQueryable<Todo> GetTodos([Service] TodoDbContext context) =>
        context.Todos.OrderBy(task => task.Status).ThenBy(task => task.SortOrder);
}
