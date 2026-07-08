using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;

namespace Datateal.Core.Tests.Deployment;

public class DiffEngineTests
{
    // ── Simple record mapper for testing ─────────────────────────────────────

    private sealed class NamedItemMapper : IResourceMapper<WorkspaceModel>
    {
        public string ResourceType => "workspace";
        public string NaturalKey(WorkspaceModel m) => m.Name;
        public bool AreEqual(WorkspaceModel desired, WorkspaceModel current) =>
            desired.Name == current.Name && desired.Description == current.Description;
    }

    private static readonly NamedItemMapper Mapper = new();

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AllCreate_WhenCurrentIsEmpty()
    {
        var desired = new[] { new WorkspaceModel { Name = "A" }, new WorkspaceModel { Name = "B" } };

        var result = DiffEngine.Diff(Mapper, desired, [], allowDeletes: false);

        Assert.Equal(2, result.Changes.Count);
        Assert.All(result.Changes, c => Assert.Equal(ChangeType.Create, c.ChangeType));
    }

    [Fact]
    public void NoChange_WhenDesiredMatchesCurrent()
    {
        var desired = new[] { new WorkspaceModel { Name = "A", Description = "hello" } };
        var current = new[] { new WorkspaceModel { Name = "A", Description = "hello" } };

        var result = DiffEngine.Diff(Mapper, desired, current, allowDeletes: false);

        var change = Assert.Single(result.Changes);
        Assert.Equal(ChangeType.NoChange, change.ChangeType);
    }

    [Fact]
    public void Update_WhenDesiredDiffersFromCurrent()
    {
        var desired = new[] { new WorkspaceModel { Name = "A", Description = "new" } };
        var current = new[] { new WorkspaceModel { Name = "A", Description = "old" } };

        var result = DiffEngine.Diff(Mapper, desired, current, allowDeletes: false);

        var change = Assert.Single(result.Changes);
        Assert.Equal(ChangeType.Update, change.ChangeType);
        Assert.Single(result.Updates);
    }

    [Fact]
    public void Delete_WhenAllowDeletesIsTrue()
    {
        var desired = new[] { new WorkspaceModel { Name = "A" } };
        var current = new[] { new WorkspaceModel { Name = "A" }, new WorkspaceModel { Name = "B" } };

        var result = DiffEngine.Diff(Mapper, desired, current, allowDeletes: true);

        Assert.Equal(2, result.Changes.Count);
        Assert.Single(result.Changes, c => c.ChangeType == ChangeType.NoChange);
        Assert.Single(result.Changes, c => c.ChangeType == ChangeType.Delete && c.ResourceName == "B");
    }

    [Fact]
    public void NoDelete_WhenAllowDeletesIsFalse()
    {
        var desired = new[] { new WorkspaceModel { Name = "A" } };
        var current = new[] { new WorkspaceModel { Name = "A" }, new WorkspaceModel { Name = "B" } };

        var result = DiffEngine.Diff(Mapper, desired, current, allowDeletes: false);

        // Only the desired item appears in result (B is silently skipped)
        Assert.Single(result.Changes);
        Assert.DoesNotContain(result.Changes, c => c.ChangeType == ChangeType.Delete);
    }

    [Fact]
    public void NaturalKeyMatchingIsCaseInsensitive()
    {
        var desired = new[] { new WorkspaceModel { Name = "SALES" } };
        var current = new[] { new WorkspaceModel { Name = "sales" } };

        var result = DiffEngine.Diff(Mapper, desired, current, allowDeletes: false);

        // The natural key ("SALES" == "sales" case-insensitively) → same resource found.
        // Content differs in Name casing → Update (not Create+Delete).
        var change = Assert.Single(result.Changes);
        Assert.Equal(ChangeType.Update, change.ChangeType);
    }

    [Fact]
    public void MixedOperations()
    {
        var desired = new[]
        {
            new WorkspaceModel { Name = "A" },              // exists, unchanged
            new WorkspaceModel { Name = "B", Description = "new-desc" }, // exists, changed
            new WorkspaceModel { Name = "C" },              // new
        };
        var current = new[]
        {
            new WorkspaceModel { Name = "A" },
            new WorkspaceModel { Name = "B", Description = "old-desc" },
            new WorkspaceModel { Name = "D" },              // only in current
        };

        var result = DiffEngine.Diff(Mapper, desired, current, allowDeletes: true);

        Assert.Equal(4, result.Changes.Count);
        Assert.Single(result.Changes, c => c.ResourceName == "A" && c.ChangeType == ChangeType.NoChange);
        Assert.Single(result.Changes, c => c.ResourceName == "B" && c.ChangeType == ChangeType.Update);
        Assert.Single(result.Changes, c => c.ResourceName == "C" && c.ChangeType == ChangeType.Create);
        Assert.Single(result.Changes, c => c.ResourceName == "D" && c.ChangeType == ChangeType.Delete);
    }

    [Fact]
    public void EmptyDesiredWithAllowDeletes_DeletesAll()
    {
        var current = new[] { new WorkspaceModel { Name = "A" }, new WorkspaceModel { Name = "B" } };

        var result = DiffEngine.Diff(Mapper, [], current, allowDeletes: true);

        Assert.Equal(2, result.Changes.Count);
        Assert.All(result.Changes, c => Assert.Equal(ChangeType.Delete, c.ChangeType));
    }
}
