using TodoBackend.Data;
using TodoBackend.Models;
using Microsoft.EntityFrameworkCore;
using TodoBackend.Services;
namespace TodoBackend.GraphQL;

public class Query
{
    public async Task<AppUser?> GetCurrentUser([Service] TodoDbContext context, [Service] AuthService authService)
    {
        var currentUser = await authService.GetCurrentUserAsync(context);
        if (currentUser == null)
        {
            return null;
        }

        return new AppUser
        {
            Id = currentUser.Id,
            Email = currentUser.Email,
            DisplayName = currentUser.DisplayName,
            PasswordHash = string.Empty,
            SessionToken = string.Empty
        };
    }

    public async Task<List<Family>> GetFamilies([Service] TodoDbContext context, [Service] AuthService authService)
    {
        var currentUser = await authService.GetCurrentUserAsync(context);
        if (currentUser == null)
        {
            return [];
        }

        if (currentUser.IsDemo)
        {
            return await context.Families.OrderBy(family => family.Name).ToListAsync();
        }

        var familyIds = await context.FamilyMemberships
            .Where(membership => membership.UserId == currentUser.Id)
            .Select(membership => membership.FamilyId)
            .ToListAsync();

        return await context.Families
            .Where(family => familyIds.Contains(family.Id))
            .OrderBy(family => family.Name)
            .ToListAsync();
    }

    public async Task<Family?> GetFamily([Service] TodoDbContext context, [Service] AuthService authService, string id)
    {
        await authService.RequireFamilyAccessAsync(context, id);
        return await context.Families.FirstOrDefaultAsync(family => family.Id == id);
    }

    public async Task<List<FamilyMember>> GetFamilyMembers([Service] TodoDbContext context, [Service] AuthService authService, string familyId)
    {
        await authService.RequireFamilyAccessAsync(context, familyId);
        return await context.FamilyMembers
            .Where(member => member.FamilyId == familyId)
            .OrderBy(member => member.Name)
            .ToListAsync();
    }

    public async Task<string> GetMyFamilyRole([Service] TodoDbContext context, [Service] AuthService authService, string familyId)
    {
        await authService.RequireFamilyAccessAsync(context, familyId);
        return await authService.GetFamilyRoleAsync(context, familyId) ?? "Member";
    }

    public async Task<List<FamilyInvite>> GetFamilyInvites([Service] TodoDbContext context, [Service] AuthService authService, string familyId)
    {
        await authService.RequireFamilyOwnerAsync(context, familyId);
        return await context.FamilyInvites
            .Where(invite => invite.FamilyId == familyId && !invite.Revoked && invite.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(invite => invite.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<FamilyAccountMember>> GetFamilyAccountMembers(
        [Service] TodoDbContext context,
        [Service] AuthService authService,
        string familyId)
    {
        await authService.RequireFamilyOwnerAsync(context, familyId);
        var memberships = await context.FamilyMemberships
            .Where(membership => membership.FamilyId == familyId)
            .OrderBy(membership => membership.CreatedAt)
            .ToListAsync();
        var userIds = memberships.Select(membership => membership.UserId).ToList();
        var users = await context.AppUsers.Where(user => userIds.Contains(user.Id)).ToListAsync();
        var userMap = users.ToDictionary(user => user.Id);

        return memberships
            .Where(membership => userMap.ContainsKey(membership.UserId))
            .Select(membership =>
            {
                var user = userMap[membership.UserId];
                return new FamilyAccountMember
                {
                    MembershipId = membership.Id,
                    UserId = user.Id,
                    DisplayName = user.DisplayName,
                    Email = user.Email,
                    Role = membership.Role
                };
            })
            .ToList();
    }

    public async Task<List<CalendarEvent>> GetCalendarEvents(
        [Service] TodoDbContext context,
        [Service] AuthService authService,
        string familyId,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        await authService.RequireFamilyAccessAsync(context, familyId);
        return await context.CalendarEvents
            .Where(calendarEvent =>
                calendarEvent.FamilyId == familyId &&
                calendarEvent.StartAt < rangeEnd &&
                calendarEvent.EndAt > rangeStart)
            .OrderBy(calendarEvent => calendarEvent.StartAt)
            .ToListAsync();
    }

    public async Task<List<Todo>> GetTasks(
        [Service] TodoDbContext context,
        [Service] AuthService authService,
        string boardId = "family-home",
        bool includeCompleted = true)
    {
        var family = await context.Families.FirstOrDefaultAsync(currentFamily => currentFamily.BoardId == boardId);
        if (family != null)
        {
            await authService.RequireFamilyAccessAsync(context, family.Id);
        }
        else
        {
            await authService.RequireCurrentUserAsync(context);
        }

        var query = context.Todos.Where(task => task.BoardId == boardId);

        if (!includeCompleted)
        {
            query = query.Where(task => !task.Completed);
        }

        return await query.OrderBy(task => task.Status).ThenBy(task => task.SortOrder).ToListAsync();
    }

    public async Task<List<Todo>> GetScheduledTasks(
        [Service] TodoDbContext context,
        [Service] AuthService authService,
        string familyId,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        await authService.RequireFamilyAccessAsync(context, familyId);
        return await context.Todos
            .Where(task =>
                task.FamilyId == familyId &&
                task.DueAt != null &&
                task.DueAt >= rangeStart &&
                task.DueAt < rangeEnd)
            .OrderBy(task => task.DueAt)
            .ToListAsync();
    }

    public async Task<List<Todo>> GetTodos([Service] TodoDbContext context, [Service] AuthService authService)
    {
        await authService.RequireCurrentUserAsync(context);
        return await context.Todos.OrderBy(task => task.Status).ThenBy(task => task.SortOrder).ToListAsync();
    }
}
