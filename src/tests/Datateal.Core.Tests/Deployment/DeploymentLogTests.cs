using Datateal.Core.Deployment;
using Xunit;

namespace Datateal.Core.Tests.Deployment;

public class DeploymentLogTests
{
    [Fact]
    public void Create_InitializesSagaInStagingStatus()
    {
        var workspaceId = Guid.NewGuid();
        var actingUserId = "usr_123456";
        var actingUserDisplayName = "Jane Doe (jane@datateal.io)";
        var saga = DeploymentLog.Create(
            workspaceId,
            DeploymentScope.Workspace,
            targetBundleJson: "{\"scope\":\"workspace\"}",
            snapshotJson: "{\"bundle\":{}}",
            issuedByUserId: actingUserId,
            issuedByDisplayName: actingUserDisplayName);

        Assert.NotEqual(Guid.Empty, saga.Id);
        Assert.Equal(workspaceId, saga.WorkspaceId);
        Assert.Equal(DeploymentScope.Workspace, saga.Scope);
        Assert.Equal(DeploymentStatus.Staging, saga.Status);
        Assert.Equal(actingUserId, saga.IssuedByUserId);
        Assert.Equal(actingUserDisplayName, saga.IssuedByDisplayName);
        Assert.Null(saga.CompletedAt);
    }

    [Fact]
    public void NormalLifecycle_TransitionsSuccessfully()
    {
        var saga = DeploymentLog.Create(
            Guid.NewGuid(),
            DeploymentScope.Workspace,
            targetBundleJson: "{}",
            snapshotJson: "{}");

        saga.TransitionToApplyingUi();
        Assert.Equal(DeploymentStatus.ApplyingUi, saga.Status);

        saga.TransitionToApplyingJobs();
        Assert.Equal(DeploymentStatus.ApplyingJobs, saga.Status);

        saga.TransitionToCompleted();
        Assert.Equal(DeploymentStatus.Completed, saga.Status);
        Assert.NotNull(saga.CompletedAt);
    }

    [Fact]
    public void InvalidTransition_FromStagingToCompleted_ThrowsInvalidOperationException()
    {
        var saga = DeploymentLog.Create(
            Guid.NewGuid(),
            DeploymentScope.Workspace,
            targetBundleJson: "{}",
            snapshotJson: "{}");

        var ex = Assert.Throws<InvalidOperationException>(() => saga.TransitionToCompleted());
        Assert.Contains("Invalid deployment state transition", ex.Message);
    }

    [Fact]
    public void RollbackLifecycle_TransitionsSuccessfully()
    {
        var saga = DeploymentLog.Create(
            Guid.NewGuid(),
            DeploymentScope.Workspace,
            targetBundleJson: "{}",
            snapshotJson: "{}");

        saga.TransitionToApplyingUi();
        saga.TransitionToRollingBack("Job apply failed");
        Assert.Equal(DeploymentStatus.RollingBack, saga.Status);
        Assert.Equal("Job apply failed", saga.FailureReason);

        saga.TransitionToRolledBack();
        Assert.Equal(DeploymentStatus.RolledBack, saga.Status);
        Assert.NotNull(saga.CompletedAt);
    }

    [Fact]
    public void CannotInitiateRollback_WhenAlreadyCompleted()
    {
        var saga = DeploymentLog.Create(
            Guid.NewGuid(),
            DeploymentScope.Workspace,
            targetBundleJson: "{}",
            snapshotJson: "{}");

        saga.TransitionToApplyingUi();
        saga.TransitionToCompleted();

        var ex = Assert.Throws<InvalidOperationException>(() => saga.TransitionToRollingBack("late error"));
        Assert.Contains("Cannot initiate rollback for deployment in status 'Completed'", ex.Message);
    }
}
