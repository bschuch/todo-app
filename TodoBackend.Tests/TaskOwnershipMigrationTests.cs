using TodoBackend.Models;
using TodoBackend.Services;
using Xunit;

namespace TodoBackend.Tests;

public class TaskOwnershipMigrationTests
{
    private static readonly Family First = new() { Id = "first", Name = "Smith", BoardId = "smith" };
    private static readonly Family Second = new() { Id = "second", Name = "Smith", BoardId = "smith" };

    [Fact]
    public void BackfillsOnlyUnambiguousBoards()
    {
        var task = new Todo { Title = "Legacy", BoardId = "smith" };
        Assert.Same(First, TaskOwnershipMigration.ResolveFamily(task, [First]));
        Assert.Null(TaskOwnershipMigration.ResolveFamily(task, [First, Second]));
        task.BoardId = "unknown";
        Assert.Null(TaskOwnershipMigration.ResolveFamily(task, [First]));
    }

    [Fact]
    public void PreservesExplicitOwnershipEvenWhenBoardsCollide()
    {
        var task = new Todo { Title = "Owned", FamilyId = Second.Id, BoardId = "smith" };
        Assert.Same(Second, TaskOwnershipMigration.ResolveFamily(task, [First, Second]));
        task.FamilyId = "deleted";
        Assert.Null(TaskOwnershipMigration.ResolveFamily(task, [First, Second]));
    }
}
