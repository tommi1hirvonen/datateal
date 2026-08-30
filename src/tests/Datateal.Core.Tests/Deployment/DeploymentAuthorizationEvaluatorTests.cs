using Datateal.Core.Deployment;
using Datateal.Deployment.Diff;
using Datateal.Ui.Server.Application.Mediator.Commands;
using Xunit;

namespace Datateal.Core.Tests.Deployment;

public class DeploymentAuthorizationEvaluatorTests
{
    private static readonly WorkspaceDeploymentGrants NoGrants = new(
        NodePoolManage: false,
        EnvironmentManage: false,
        JobManage: false);

    private static readonly WorkspaceDeploymentGrants AllGrants = new(
        NodePoolManage: true,
        EnvironmentManage: true,
        JobManage: true);

    private static ChangeSet WorkspaceChangeSet(params ResourceChange[] changes) => new()
    {
        Scope = "workspace",
        Target = "test-workspace",
        DryRun = true,
        Changes = [.. changes],
    };

    private static ResourceChange Change(string resourceType, ChangeType changeType) => new()
    {
        ResourceType = resourceType,
        ResourceName = "example",
        ChangeType = changeType,
    };

    [Fact]
    public void EnsureAuthorized_Passes_WhenNoChangesAtAll()
    {
        var workspaceChanges = WorkspaceChangeSet();

        var ex = Record.Exception(() =>
            DeploymentAuthorizationEvaluator.EnsureAuthorized(workspaceChanges, jobChanges: null, NoGrants));

        Assert.Null(ex);
    }

    [Fact]
    public void EnsureAuthorized_Passes_WhenOnlyNoChangeEntriesPresent()
    {
        var workspaceChanges = WorkspaceChangeSet(
            Change("node_pool", ChangeType.NoChange),
            Change("secret", ChangeType.NoChange),
            Change("notebook", ChangeType.Update));
        var jobChanges = WorkspaceChangeSet(Change("job", ChangeType.NoChange));

        var ex = Record.Exception(() =>
            DeploymentAuthorizationEvaluator.EnsureAuthorized(workspaceChanges, jobChanges, NoGrants));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData(ChangeType.Create)]
    [InlineData(ChangeType.Update)]
    [InlineData(ChangeType.Delete)]
    public void EnsureAuthorized_Throws_WhenNodePoolChangesWithoutNodePoolManage(ChangeType changeType)
    {
        var workspaceChanges = WorkspaceChangeSet(Change("node_pool", changeType));

        var ex = Assert.Throws<DeploymentAuthorizationException>(() =>
            DeploymentAuthorizationEvaluator.EnsureAuthorized(workspaceChanges, jobChanges: null, NoGrants));

        Assert.Contains("NodePoolManage", ex.Message);
    }

    [Fact]
    public void EnsureAuthorized_Passes_WhenNodePoolChangesWithNodePoolManageGrant()
    {
        var workspaceChanges = WorkspaceChangeSet(Change("node_pool", ChangeType.Create));
        var grants = NoGrants with { NodePoolManage = true };

        var ex = Record.Exception(() =>
            DeploymentAuthorizationEvaluator.EnsureAuthorized(workspaceChanges, jobChanges: null, grants));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData("environment_variable")]
    [InlineData("secret")]
    [InlineData("wheel_package")]
    public void EnsureAuthorized_Throws_WhenEnvironmentResourceChangesWithoutEnvironmentManage(string resourceType)
    {
        var workspaceChanges = WorkspaceChangeSet(Change(resourceType, ChangeType.Create));

        var ex = Assert.Throws<DeploymentAuthorizationException>(() =>
            DeploymentAuthorizationEvaluator.EnsureAuthorized(workspaceChanges, jobChanges: null, NoGrants));

        Assert.Contains("EnvironmentManage", ex.Message);
    }

    [Fact]
    public void EnsureAuthorized_Throws_WhenJobChangesWithoutJobManage()
    {
        var workspaceChanges = WorkspaceChangeSet();
        var jobChanges = WorkspaceChangeSet(Change("job", ChangeType.Create));

        var ex = Assert.Throws<DeploymentAuthorizationException>(() =>
            DeploymentAuthorizationEvaluator.EnsureAuthorized(workspaceChanges, jobChanges, NoGrants));

        Assert.Contains("JobManage", ex.Message);
    }

    [Fact]
    public void EnsureAuthorized_Passes_WhenNotebookAndQueryChangesOnly()
    {
        // Folders/notebooks/queries are governed by the baseline WorkspaceManage policy, already
        // enforced by the controller's [Authorize] attribute — no extra grant should be required.
        var workspaceChanges = WorkspaceChangeSet(
            Change("folder", ChangeType.Create),
            Change("notebook", ChangeType.Create),
            Change("query", ChangeType.Delete));

        var ex = Record.Exception(() =>
            DeploymentAuthorizationEvaluator.EnsureAuthorized(workspaceChanges, jobChanges: null, NoGrants));

        Assert.Null(ex);
    }

    [Fact]
    public void EnsureAuthorized_ListsAllMissingPermissions_WhenMultipleResourceTypesChanged()
    {
        var workspaceChanges = WorkspaceChangeSet(
            Change("node_pool", ChangeType.Create),
            Change("secret", ChangeType.Update));
        var jobChanges = WorkspaceChangeSet(Change("job", ChangeType.Delete));

        var ex = Assert.Throws<DeploymentAuthorizationException>(() =>
            DeploymentAuthorizationEvaluator.EnsureAuthorized(workspaceChanges, jobChanges, NoGrants));

        Assert.Contains("NodePoolManage", ex.Message);
        Assert.Contains("EnvironmentManage", ex.Message);
        Assert.Contains("JobManage", ex.Message);
    }

    [Fact]
    public void EnsureAuthorized_Passes_WhenAllGrantsPresentRegardlessOfChanges()
    {
        var workspaceChanges = WorkspaceChangeSet(
            Change("node_pool", ChangeType.Create),
            Change("secret", ChangeType.Update),
            Change("wheel_package", ChangeType.Delete));
        var jobChanges = WorkspaceChangeSet(Change("job", ChangeType.Create));

        var ex = Record.Exception(() =>
            DeploymentAuthorizationEvaluator.EnsureAuthorized(workspaceChanges, jobChanges, AllGrants));

        Assert.Null(ex);
    }
}
