using Microsoft.EntityFrameworkCore;
using TodoBackend.Data;
using TodoBackend.Models;
using MongoDB.Bson;
using WorkflowTaskStatus = TodoBackend.Models.TaskStatus;

namespace TodoBackend.GraphQL;

public class Mutation
{
    public async Task<Family> CreateFamily(string name, [Service] TodoDbContext dbContext)
    {
        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new GraphQLException("Family name is required.");
        }

        var now = DateTime.UtcNow;
        var family = new Family
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = normalizedName,
            BoardId = NormalizeBoardId(normalizedName),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Families.Add(family);
        await dbContext.SaveChangesAsync();
        return family;
    }

    public async Task<FamilyMember> CreateFamilyMember(
        string familyId,
        string name,
        string color,
        [Service] TodoDbContext dbContext)
    {
        var family = await dbContext.Families.FirstOrDefaultAsync(currentFamily => currentFamily.Id == familyId);
        if (family == null)
        {
            throw new GraphQLException("Family was not found.");
        }

        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new GraphQLException("Member name is required.");
        }

        var now = DateTime.UtcNow;
        var member = new FamilyMember
        {
            Id = ObjectId.GenerateNewId().ToString(),
            FamilyId = familyId,
            Name = normalizedName,
            Color = NormalizeColor(color),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.FamilyMembers.Add(member);
        await dbContext.SaveChangesAsync();
        return member;
    }

    public async Task<bool> DeleteFamily(string familyId, [Service] TodoDbContext dbContext)
    {
        var family = await dbContext.Families.FirstOrDefaultAsync(currentFamily => currentFamily.Id == familyId);
        if (family == null)
        {
            return false;
        }

        var events = await dbContext.CalendarEvents.Where(calendarEvent => calendarEvent.FamilyId == familyId).ToListAsync();
        var members = await dbContext.FamilyMembers.Where(member => member.FamilyId == familyId).ToListAsync();
        var tasks = await dbContext.Todos.Where(task => task.BoardId == family.BoardId).ToListAsync();

        dbContext.CalendarEvents.RemoveRange(events);
        dbContext.FamilyMembers.RemoveRange(members);
        dbContext.Todos.RemoveRange(tasks);
        dbContext.Families.Remove(family);

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<FamilyMember?> UpdateFamilyMember(
        string memberId,
        string name,
        string color,
        [Service] TodoDbContext dbContext)
    {
        var member = await dbContext.FamilyMembers.FirstOrDefaultAsync(currentMember => currentMember.Id == memberId);
        if (member == null)
        {
            return null;
        }

        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new GraphQLException("Member name is required.");
        }

        member.Name = normalizedName;
        member.Color = NormalizeColor(color);
        member.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return member;
    }

    public async Task<CalendarEvent> CreateCalendarEvent(
        string familyId,
        string? memberId,
        string title,
        DateTime startAt,
        DateTime endAt,
        string? notes,
        [Service] TodoDbContext dbContext)
    {
        await ValidateCalendarEventAsync(dbContext, familyId, memberId, title, startAt, endAt);

        var now = DateTime.UtcNow;
        var calendarEvent = new CalendarEvent
        {
            Id = ObjectId.GenerateNewId().ToString(),
            FamilyId = familyId,
            MemberId = string.IsNullOrWhiteSpace(memberId) ? null : memberId,
            Title = title.Trim(),
            StartAt = startAt.ToUniversalTime(),
            EndAt = endAt.ToUniversalTime(),
            Notes = notes?.Trim() ?? string.Empty,
            Tone = await ResolveMemberToneAsync(dbContext, memberId),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.CalendarEvents.Add(calendarEvent);
        await dbContext.SaveChangesAsync();
        return calendarEvent;
    }

    public async Task<CalendarEvent?> UpdateCalendarEvent(
        string eventId,
        string? memberId,
        string title,
        DateTime startAt,
        DateTime endAt,
        string? notes,
        [Service] TodoDbContext dbContext)
    {
        var calendarEvent = await dbContext.CalendarEvents.FirstOrDefaultAsync(currentEvent => currentEvent.Id == eventId);
        if (calendarEvent == null)
        {
            return null;
        }

        await ValidateCalendarEventAsync(dbContext, calendarEvent.FamilyId, memberId, title, startAt, endAt);

        calendarEvent.MemberId = string.IsNullOrWhiteSpace(memberId) ? null : memberId;
        calendarEvent.Title = title.Trim();
        calendarEvent.StartAt = startAt.ToUniversalTime();
        calendarEvent.EndAt = endAt.ToUniversalTime();
        calendarEvent.Notes = notes?.Trim() ?? string.Empty;
        calendarEvent.Tone = await ResolveMemberToneAsync(dbContext, memberId);
        calendarEvent.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return calendarEvent;
    }

    public async Task<bool> DeleteCalendarEvent(string eventId, [Service] TodoDbContext dbContext)
    {
        var calendarEvent = await dbContext.CalendarEvents.FirstOrDefaultAsync(currentEvent => currentEvent.Id == eventId);
        if (calendarEvent == null)
        {
            return false;
        }

        dbContext.CalendarEvents.Remove(calendarEvent);
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<Todo> CreateTask(
        string title,
        string assigneeName,
        [Service] TodoDbContext dbContext,
        string? familyId = null,
        string boardId = "family-home",
        WorkflowTaskStatus status = WorkflowTaskStatus.Todo,
        DateTime? dueAt = null,
        int? durationMinutes = null,
        string? recurrenceRule = null)
    {
        var normalizedTitle = title.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            throw new GraphQLException("Task title is required.");
        }

        await ValidateTaskScheduleAsync(dbContext, familyId, dueAt, durationMinutes);

        var nextOrder = await GetNextSortOrderAsync(dbContext, boardId, status);
        var task = new Todo
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Title = normalizedTitle,
            FamilyId = string.IsNullOrWhiteSpace(familyId) ? null : familyId,
            AssigneeName = assigneeName.Trim(),
            BoardId = boardId,
            Status = status,
            SortOrder = nextOrder,
            DueAt = dueAt?.ToUniversalTime(),
            DurationMinutes = durationMinutes,
            RecurrenceRule = NormalizeRecurrenceRule(recurrenceRule),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Completed = status == WorkflowTaskStatus.Done
        };

        dbContext.Todos.Add(task);
        await dbContext.SaveChangesAsync();
        return task;
    }

    public async Task<Todo?> UpdateTaskSchedule(
        string taskId,
        DateTime? dueAt,
        int? durationMinutes,
        string? recurrenceRule,
        [Service] TodoDbContext dbContext)
    {
        var task = await dbContext.Todos.FirstOrDefaultAsync(currentTask => currentTask.Id == taskId);
        if (task == null)
        {
            return null;
        }

        await ValidateTaskScheduleAsync(dbContext, task.FamilyId, dueAt, durationMinutes);

        task.DueAt = dueAt?.ToUniversalTime();
        task.DurationMinutes = durationMinutes;
        task.RecurrenceRule = NormalizeRecurrenceRule(recurrenceRule);
        task.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return task;
    }

    public async Task<Todo?> ToggleTaskCompletion(string taskId, [Service] TodoDbContext dbContext)
    {
        var task = await dbContext.Todos.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            return null;
        }

        var sourceStatus = task.Status;
        task.Completed = !task.Completed;
        task.Status = task.Completed ? WorkflowTaskStatus.Done : WorkflowTaskStatus.Todo;
        task.UpdatedAt = DateTime.UtcNow;

        await NormalizeSortOrdersAsync(dbContext, task.BoardId, sourceStatus, task.Id, null);
        await NormalizeSortOrdersAsync(dbContext, task.BoardId, task.Status, task.Id, null);
        await dbContext.SaveChangesAsync();
        return task;
    }

    public async Task<Todo?> MoveTask(
        string taskId,
        WorkflowTaskStatus targetStatus,
        int targetOrder,
        [Service] TodoDbContext dbContext)
    {
        var task = await dbContext.Todos.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            return null;
        }

        var sourceStatus = task.Status;
        var boardId = task.BoardId;

        task.Status = targetStatus;
        task.Completed = targetStatus == WorkflowTaskStatus.Done;
        task.UpdatedAt = DateTime.UtcNow;

        await NormalizeSortOrdersAsync(dbContext, boardId, sourceStatus, task.Id, null);
        await NormalizeSortOrdersAsync(dbContext, boardId, targetStatus, task.Id, targetOrder);

        await dbContext.SaveChangesAsync();
        return task;
    }

    public async Task<bool> DeleteTask(string taskId, [Service] TodoDbContext dbContext)
    {
        var task = await dbContext.Todos.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            return false;
        }

        var boardId = task.BoardId;
        var status = task.Status;

        dbContext.Todos.Remove(task);
        await dbContext.SaveChangesAsync();

        // Don't pass movingTaskId - just normalize the remaining tasks
        await NormalizeSortOrdersAsync(dbContext, boardId, status, null, null);
        await dbContext.SaveChangesAsync();
        return true;
    }

    private static async Task<int> GetNextSortOrderAsync(TodoDbContext dbContext, string boardId, WorkflowTaskStatus status)
    {
        var highestSortOrder = await dbContext.Todos
            .Where(task => task.BoardId == boardId && task.Status == status)
            .OrderByDescending(task => task.SortOrder)
            .Select(task => (int?)task.SortOrder)
            .FirstOrDefaultAsync();

        return (highestSortOrder ?? -1) + 1;
    }

    private static async Task NormalizeSortOrdersAsync(
        TodoDbContext dbContext,
        string boardId,
        WorkflowTaskStatus status,
        string? movingTaskId,
        int? targetOrder)
    {
        var tasks = await dbContext.Todos
            .Where(task => task.BoardId == boardId && task.Status == status && (movingTaskId == null || task.Id != movingTaskId))
            .OrderBy(task => task.SortOrder)
            .ToListAsync();

        var insertIndex = targetOrder.HasValue
            ? Math.Clamp(targetOrder.Value, 0, tasks.Count)
            : tasks.Count;

        Todo? movingTask = null;
        if (!string.IsNullOrWhiteSpace(movingTaskId))
        {
            movingTask = await dbContext.Todos.FirstOrDefaultAsync(task => task.Id == movingTaskId);
        }

        if (movingTask != null && movingTask.Status == status)
        {
            tasks.Insert(insertIndex, movingTask);
        }

        for (var index = 0; index < tasks.Count; index++)
        {
            tasks[index].SortOrder = index;
            tasks[index].UpdatedAt = DateTime.UtcNow;
        }
    }

    private static async Task ValidateCalendarEventAsync(
        TodoDbContext dbContext,
        string familyId,
        string? memberId,
        string title,
        DateTime startAt,
        DateTime endAt)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new GraphQLException("Event title is required.");
        }

        if (endAt <= startAt)
        {
            throw new GraphQLException("Event end time must be after the start time.");
        }

        var familyExists = await dbContext.Families.AnyAsync(family => family.Id == familyId);
        if (!familyExists)
        {
            throw new GraphQLException("Family was not found.");
        }

        if (!string.IsNullOrWhiteSpace(memberId))
        {
            var memberExists = await dbContext.FamilyMembers.AnyAsync(member => member.Id == memberId && member.FamilyId == familyId);
            if (!memberExists)
            {
                throw new GraphQLException("Member was not found for this family.");
            }
        }
    }

    private static async Task ValidateTaskScheduleAsync(TodoDbContext dbContext, string? familyId, DateTime? dueAt, int? durationMinutes)
    {
        if (!string.IsNullOrWhiteSpace(familyId))
        {
            var familyExists = await dbContext.Families.AnyAsync(family => family.Id == familyId);
            if (!familyExists)
            {
                throw new GraphQLException("Family was not found.");
            }
        }

        if (dueAt == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(familyId))
        {
            throw new GraphQLException("Scheduled tasks require a family.");
        }

        if (durationMinutes is <= 0 or > 1440)
        {
            throw new GraphQLException("Task duration must be between 1 and 1440 minutes.");
        }
    }

    private static async Task<string> ResolveMemberToneAsync(TodoDbContext dbContext, string? memberId)
    {
        if (string.IsNullOrWhiteSpace(memberId))
        {
            return "teal";
        }

        var member = await dbContext.FamilyMembers.FirstOrDefaultAsync(currentMember => currentMember.Id == memberId);
        return member?.Color ?? "teal";
    }

    private static string NormalizeBoardId(string familyName)
    {
        var normalized = new string(familyName
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

        normalized = string.Join('-', normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(normalized) ? $"family-{ObjectId.GenerateNewId()}" : normalized;
    }

    private static string NormalizeColor(string color)
    {
        var normalized = color.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "#6dbec2" : normalized;
    }

    private static string NormalizeRecurrenceRule(string? recurrenceRule)
    {
        var normalized = recurrenceRule?.Trim();
        return normalized is "Daily" or "Weekly" or "Monthly" ? normalized : "None";
    }
}
