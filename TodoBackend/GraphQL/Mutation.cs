using Microsoft.EntityFrameworkCore;
using TodoBackend.Data;
using TodoBackend.Models;
using MongoDB.Bson;
using TodoBackend.Services;
using WorkflowTaskStatus = TodoBackend.Models.TaskStatus;

namespace TodoBackend.GraphQL;

public class Mutation
{
    public async Task<AuthPayload> SignUp(
        string email,
        string displayName,
        string password,
        [Service] TodoDbContext dbContext)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedName = displayName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(normalizedName) || password.Length < 8)
        {
            throw new GraphQLException("Email, display name, and an 8 character password are required.");
        }

        var existingUser = await dbContext.AppUsers.FirstOrDefaultAsync(user => user.Email == normalizedEmail);
        if (existingUser != null)
        {
            throw new GraphQLException("An account already exists for that email.");
        }

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Email = normalizedEmail,
            DisplayName = normalizedName,
            PasswordHash = AuthService.HashPassword(password),
            SessionToken = AuthService.CreateSessionToken(),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.AppUsers.Add(user);
        await dbContext.SaveChangesAsync();
        return new AuthPayload { User = user, Token = user.SessionToken };
    }

    public async Task<AuthPayload> SignIn(string email, string password, [Service] TodoDbContext dbContext)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await dbContext.AppUsers.FirstOrDefaultAsync(currentUser => currentUser.Email == normalizedEmail);
        if (user == null || !AuthService.VerifyPassword(password, user.PasswordHash))
        {
            throw new GraphQLException("Email or password was incorrect.");
        }

        user.SessionToken = AuthService.CreateSessionToken();
        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        return new AuthPayload { User = user, Token = user.SessionToken };
    }

    public async Task<Family> CreateFamily(string name, string? color, [Service] TodoDbContext dbContext, [Service] AuthService authService)
    {
        var currentUser = await authService.RequireCurrentUserAsync(dbContext);
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
            Color = NormalizeColor(color ?? "#3479b5"),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Families.Add(family);
        await dbContext.SaveChangesAsync();

        if (!currentUser.IsDemo)
        {
            dbContext.FamilyMemberships.Add(new FamilyMembership
            {
                Id = ObjectId.GenerateNewId().ToString(),
                FamilyId = family.Id,
                UserId = currentUser.Id,
                Role = "Owner",
                CreatedAt = now
            });
            await dbContext.SaveChangesAsync();
        }

        return family;
    }

    public async Task<Family?> UpdateFamily(
        string familyId,
        string name,
        string color,
        [Service] TodoDbContext dbContext,
        [Service] AuthService authService)
    {
        await authService.RequireFamilyOwnerAsync(dbContext, familyId);
        var family = await dbContext.Families.FirstOrDefaultAsync(currentFamily => currentFamily.Id == familyId);
        if (family == null)
        {
            return null;
        }

        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new GraphQLException("Family name is required.");
        }

        family.Name = normalizedName;
        family.Color = NormalizeColor(color);
        family.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return family;
    }

    public async Task<FamilyInvite> CreateFamilyInvite(string familyId, [Service] TodoDbContext dbContext, [Service] AuthService authService)
    {
        var currentUser = await authService.RequireCurrentUserAsync(dbContext);
        await authService.RequireFamilyOwnerAsync(dbContext, familyId);

        var invite = new FamilyInvite
        {
            Id = ObjectId.GenerateNewId().ToString(),
            FamilyId = familyId,
            Code = CreateInviteCode(),
            CreatedByUserId = currentUser.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(14)
        };

        dbContext.FamilyInvites.Add(invite);
        await dbContext.SaveChangesAsync();
        return invite;
    }

    public async Task<bool> RevokeFamilyInvite(string inviteId, [Service] TodoDbContext dbContext, [Service] AuthService authService)
    {
        var invite = await dbContext.FamilyInvites.FirstOrDefaultAsync(currentInvite => currentInvite.Id == inviteId);
        if (invite == null)
        {
            return false;
        }

        await authService.RequireFamilyOwnerAsync(dbContext, invite.FamilyId);
        invite.Revoked = true;
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<FamilyAccountMember?> UpdateFamilyAccountRole(
        string membershipId,
        string role,
        [Service] TodoDbContext dbContext,
        [Service] AuthService authService)
    {
        var membership = await dbContext.FamilyMemberships.FirstOrDefaultAsync(currentMembership => currentMembership.Id == membershipId);
        if (membership == null)
        {
            return null;
        }

        await authService.RequireFamilyOwnerAsync(dbContext, membership.FamilyId);
        var normalizedRole = NormalizeFamilyRole(role);
        if (membership.Role == "Owner" && normalizedRole != "Owner")
        {
            await EnsureAnotherOwnerExistsAsync(dbContext, membership);
        }

        membership.Role = normalizedRole;
        await dbContext.SaveChangesAsync();
        return await BuildFamilyAccountMemberAsync(dbContext, membership);
    }

    public async Task<bool> RemoveFamilyAccountMember(
        string membershipId,
        [Service] TodoDbContext dbContext,
        [Service] AuthService authService)
    {
        var membership = await dbContext.FamilyMemberships.FirstOrDefaultAsync(currentMembership => currentMembership.Id == membershipId);
        if (membership == null)
        {
            return false;
        }

        await authService.RequireFamilyOwnerAsync(dbContext, membership.FamilyId);
        var currentUser = await authService.RequireCurrentUserAsync(dbContext);
        if (membership.UserId == currentUser.Id)
        {
            throw new GraphQLException("Owners cannot remove their own account from the family.");
        }

        if (membership.Role == "Owner")
        {
            await EnsureAnotherOwnerExistsAsync(dbContext, membership);
        }

        dbContext.FamilyMemberships.Remove(membership);
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<Family> AcceptFamilyInvite(string code, [Service] TodoDbContext dbContext, [Service] AuthService authService)
    {
        var currentUser = await authService.RequireCurrentUserAsync(dbContext);
        var normalizedCode = code.Trim().ToUpperInvariant();
        var invite = await dbContext.FamilyInvites.FirstOrDefaultAsync(currentInvite => currentInvite.Code == normalizedCode);
        if (invite == null || invite.Revoked || invite.ExpiresAt <= DateTime.UtcNow)
        {
            throw new GraphQLException("Invite code is invalid or expired.");
        }

        if (!currentUser.IsDemo && !await dbContext.FamilyMemberships.AnyAsync(membership => membership.UserId == currentUser.Id && membership.FamilyId == invite.FamilyId))
        {
            dbContext.FamilyMemberships.Add(new FamilyMembership
            {
                Id = ObjectId.GenerateNewId().ToString(),
                FamilyId = invite.FamilyId,
                UserId = currentUser.Id,
                Role = "Member",
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        return await dbContext.Families.FirstAsync(family => family.Id == invite.FamilyId);
    }

    public async Task<FamilyMember> CreateFamilyMember(
        string familyId,
        string name,
        string color,
        [Service] TodoDbContext dbContext,
        [Service] AuthService authService)
    {
        await authService.RequireFamilyOwnerAsync(dbContext, familyId);
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

    public async Task<bool> DeleteFamily(string familyId, [Service] TodoDbContext dbContext, [Service] AuthService authService)
    {
        await authService.RequireFamilyOwnerAsync(dbContext, familyId);
        var family = await dbContext.Families.FirstOrDefaultAsync(currentFamily => currentFamily.Id == familyId);
        if (family == null)
        {
            return false;
        }

        var events = await dbContext.CalendarEvents.Where(calendarEvent => calendarEvent.FamilyId == familyId).ToListAsync();
        var members = await dbContext.FamilyMembers.Where(member => member.FamilyId == familyId).ToListAsync();
        var tasks = await dbContext.Todos.Where(task => task.BoardId == family.BoardId).ToListAsync();
        var memberships = await dbContext.FamilyMemberships.Where(membership => membership.FamilyId == familyId).ToListAsync();
        var invites = await dbContext.FamilyInvites.Where(invite => invite.FamilyId == familyId).ToListAsync();

        dbContext.CalendarEvents.RemoveRange(events);
        dbContext.FamilyMembers.RemoveRange(members);
        dbContext.Todos.RemoveRange(tasks);
        dbContext.FamilyMemberships.RemoveRange(memberships);
        dbContext.FamilyInvites.RemoveRange(invites);
        dbContext.Families.Remove(family);

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<FamilyMember?> UpdateFamilyMember(
        string memberId,
        string name,
        string color,
        [Service] TodoDbContext dbContext,
        [Service] AuthService authService)
    {
        var member = await dbContext.FamilyMembers.FirstOrDefaultAsync(currentMember => currentMember.Id == memberId);
        if (member == null)
        {
            return null;
        }

        await authService.RequireFamilyOwnerAsync(dbContext, member.FamilyId);

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
        List<string>? memberIds,
        string title,
        DateTime startAt,
        DateTime endAt,
        bool isAllDay,
        string? notes,
        [Service] TodoDbContext dbContext,
        [Service] AuthService authService)
    {
        await authService.RequireFamilyAccessAsync(dbContext, familyId);
        var normalizedMemberIds = NormalizeEventMemberIds(memberId, memberIds);
        await ValidateCalendarEventAsync(dbContext, familyId, normalizedMemberIds, title, startAt, endAt);

        var now = DateTime.UtcNow;
        var calendarEvent = new CalendarEvent
        {
            Id = ObjectId.GenerateNewId().ToString(),
            FamilyId = familyId,
            MemberId = normalizedMemberIds.FirstOrDefault(),
            MemberIds = normalizedMemberIds,
            Title = title.Trim(),
            StartAt = startAt.ToUniversalTime(),
            EndAt = endAt.ToUniversalTime(),
            IsAllDay = isAllDay,
            Notes = notes?.Trim() ?? string.Empty,
            Tone = await ResolveMemberToneAsync(dbContext, normalizedMemberIds.FirstOrDefault()),
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
        List<string>? memberIds,
        string title,
        DateTime startAt,
        DateTime endAt,
        bool isAllDay,
        string? notes,
        [Service] TodoDbContext dbContext,
        [Service] AuthService authService)
    {
        var calendarEvent = await dbContext.CalendarEvents.FirstOrDefaultAsync(currentEvent => currentEvent.Id == eventId);
        if (calendarEvent == null)
        {
            return null;
        }

        await authService.RequireFamilyAccessAsync(dbContext, calendarEvent.FamilyId);
        var normalizedMemberIds = NormalizeEventMemberIds(memberId, memberIds);
        await ValidateCalendarEventAsync(dbContext, calendarEvent.FamilyId, normalizedMemberIds, title, startAt, endAt);

        calendarEvent.MemberId = normalizedMemberIds.FirstOrDefault();
        calendarEvent.MemberIds = normalizedMemberIds;
        calendarEvent.Title = title.Trim();
        calendarEvent.StartAt = startAt.ToUniversalTime();
        calendarEvent.EndAt = endAt.ToUniversalTime();
        calendarEvent.IsAllDay = isAllDay;
        calendarEvent.Notes = notes?.Trim() ?? string.Empty;
        calendarEvent.Tone = await ResolveMemberToneAsync(dbContext, normalizedMemberIds.FirstOrDefault());
        calendarEvent.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return calendarEvent;
    }

    public async Task<bool> DeleteCalendarEvent(string eventId, [Service] TodoDbContext dbContext, [Service] AuthService authService)
    {
        var calendarEvent = await dbContext.CalendarEvents.FirstOrDefaultAsync(currentEvent => currentEvent.Id == eventId);
        if (calendarEvent == null)
        {
            return false;
        }

        await authService.RequireFamilyAccessAsync(dbContext, calendarEvent.FamilyId);
        dbContext.CalendarEvents.Remove(calendarEvent);
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<Todo> CreateTask(
        string title,
        string assigneeName,
        [Service] TodoDbContext dbContext,
        [Service] AuthService authService,
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

        await RequireTaskFamilyAccessAsync(dbContext, authService, familyId, boardId);
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
        [Service] TodoDbContext dbContext,
        [Service] AuthService authService)
    {
        var task = await dbContext.Todos.FirstOrDefaultAsync(currentTask => currentTask.Id == taskId);
        if (task == null)
        {
            return null;
        }

        await RequireTaskFamilyAccessAsync(dbContext, authService, task.FamilyId, task.BoardId);
        await ValidateTaskScheduleAsync(dbContext, task.FamilyId, dueAt, durationMinutes);

        task.DueAt = dueAt?.ToUniversalTime();
        task.DurationMinutes = durationMinutes;
        task.RecurrenceRule = NormalizeRecurrenceRule(recurrenceRule);
        task.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return task;
    }

    public async Task<Todo?> ToggleTaskCompletion(string taskId, [Service] TodoDbContext dbContext, [Service] AuthService authService)
    {
        var task = await dbContext.Todos.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            return null;
        }

        await RequireTaskFamilyAccessAsync(dbContext, authService, task.FamilyId, task.BoardId);
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
        [Service] TodoDbContext dbContext,
        [Service] AuthService authService)
    {
        var task = await dbContext.Todos.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            return null;
        }

        await RequireTaskFamilyAccessAsync(dbContext, authService, task.FamilyId, task.BoardId);
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

    public async Task<bool> DeleteTask(string taskId, [Service] TodoDbContext dbContext, [Service] AuthService authService)
    {
        var task = await dbContext.Todos.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            return false;
        }

        await RequireTaskFamilyAccessAsync(dbContext, authService, task.FamilyId, task.BoardId);
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
        List<string> memberIds,
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

        if (memberIds.Count > 0)
        {
            var matchingMemberCount = await dbContext.FamilyMembers.CountAsync(member => memberIds.Contains(member.Id) && member.FamilyId == familyId);
            if (matchingMemberCount != memberIds.Count)
            {
                throw new GraphQLException("One or more members were not found for this family.");
            }
        }
    }

    private static List<string> NormalizeEventMemberIds(string? memberId, List<string>? memberIds)
    {
        var selectedMemberIds = memberIds ?? (string.IsNullOrWhiteSpace(memberId) ? new List<string>() : new List<string> { memberId });
        return selectedMemberIds
            .Select(currentMemberId => currentMemberId.Trim())
            .Where(currentMemberId => !string.IsNullOrWhiteSpace(currentMemberId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
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

    private static async Task RequireTaskFamilyAccessAsync(TodoDbContext dbContext, AuthService authService, string? familyId, string boardId)
    {
        if (!string.IsNullOrWhiteSpace(familyId))
        {
            await authService.RequireFamilyAccessAsync(dbContext, familyId);
            return;
        }

        var family = await dbContext.Families.FirstOrDefaultAsync(currentFamily => currentFamily.BoardId == boardId);
        if (family != null)
        {
            await authService.RequireFamilyAccessAsync(dbContext, family.Id);
            return;
        }

        await authService.RequireCurrentUserAsync(dbContext);
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

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string NormalizeFamilyRole(string role) =>
        string.Equals(role.Trim(), "Owner", StringComparison.OrdinalIgnoreCase) ? "Owner" : "Member";

    private static string NormalizeRecurrenceRule(string? recurrenceRule)
    {
        var normalized = recurrenceRule?.Trim();
        return normalized is "Daily" or "Weekly" or "Monthly" ? normalized : "None";
    }

    private static string CreateInviteCode()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", string.Empty, StringComparison.Ordinal)
            .Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("=", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant()[..10];
    }

    private static async Task EnsureAnotherOwnerExistsAsync(TodoDbContext dbContext, FamilyMembership membership)
    {
        var anotherOwnerExists = await dbContext.FamilyMemberships.AnyAsync(currentMembership =>
            currentMembership.FamilyId == membership.FamilyId &&
            currentMembership.Id != membership.Id &&
            currentMembership.Role == "Owner");
        if (!anotherOwnerExists)
        {
            throw new GraphQLException("A family must keep at least one owner.");
        }
    }

    private static async Task<FamilyAccountMember?> BuildFamilyAccountMemberAsync(TodoDbContext dbContext, FamilyMembership membership)
    {
        var user = await dbContext.AppUsers.FirstOrDefaultAsync(currentUser => currentUser.Id == membership.UserId);
        if (user == null)
        {
            return null;
        }

        return new FamilyAccountMember
        {
            MembershipId = membership.Id,
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Role = membership.Role
        };
    }
}
