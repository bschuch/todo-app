using Microsoft.EntityFrameworkCore;
using TodoBackend.Data;
using TodoBackend.Models;

namespace TodoBackend.Services;

public static class TaskOwnershipMigration
{
    public static Family? ResolveFamily(Todo task, IReadOnlyList<Family> families)
    {
        // An existing ownership value must never be reassigned by guessing from a board.
        if (!string.IsNullOrWhiteSpace(task.FamilyId))
            return families.SingleOrDefault(family => family.Id == task.FamilyId);
        var matches = families.Where(family => family.BoardId == task.BoardId).Take(2).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    public static async Task RunAsync(TodoDbContext context, ILogger logger, bool apply)
    {
        var families = await context.Families.ToListAsync();
        var tasks = await context.Todos.ToListAsync();
        var changed = 0;
        var unresolved = 0;
        foreach (var task in tasks)
        {
            var family = ResolveFamily(task, families);
            if (family == null)
            {
                unresolved++;
                logger.LogWarning("Unresolved task ownership: {TaskId}", task.Id);
                continue;
            }
            if (task.FamilyId == family.Id && task.BoardId == family.BoardId) continue;
            changed++;
            task.FamilyId = family.Id;
            task.BoardId = family.BoardId;
        }
        if (apply) await context.SaveChangesAsync();
        logger.LogInformation("Task ownership migration: apply={Apply}, changes={Changes}, unresolved={Unresolved}",
            apply, changed, unresolved);
    }
}
