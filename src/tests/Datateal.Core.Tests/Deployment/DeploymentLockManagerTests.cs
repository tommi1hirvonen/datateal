using Datateal.Core.Deployment;
using Datateal.Ui.Server.Core.Deployment;
using Datateal.Ui.Server.Infrastructure.Deployment;
using Xunit;

namespace Datateal.Core.Tests.Deployment;

public class DeploymentLockManagerTests
{
    [Fact]
    public void AcquireLock_SecondCallForSameKey_ThrowsDeploymentConflictException()
    {
        var manager = new DeploymentLockManager();

        using var first = manager.AcquireLock("workspace:1", "workspace '1'");

        var ex = Assert.Throws<DeploymentConflictException>(() => manager.AcquireLock("workspace:1", "workspace '1'"));
        Assert.Contains("workspace '1'", ex.Message);
        Assert.Contains("already in progress", ex.Message);
    }

    [Fact]
    public void AcquireLock_DifferentKeys_BothSucceedConcurrently()
    {
        var manager = new DeploymentLockManager();

        using var first = manager.AcquireLock("workspace:1", "workspace '1'");
        using var second = manager.AcquireLock("workspace:2", "workspace '2'");
    }

    [Fact]
    public void AcquireLock_AfterDisposingPreviousHolder_SucceedsAgain()
    {
        var manager = new DeploymentLockManager();

        var first = manager.AcquireLock("workspace:1", "workspace '1'");
        first.Dispose();

        using var second = manager.AcquireLock("workspace:1", "workspace '1'");
    }

    [Fact]
    public void AcquireLock_DisposingTwiceDoesNotReleaseTwice()
    {
        var manager = new DeploymentLockManager();

        var first = manager.AcquireLock("workspace:1", "workspace '1'");
        first.Dispose();
        first.Dispose(); // must be a no-op, not an over-release that unblocks a second concurrent holder

        using var second = manager.AcquireLock("workspace:1", "workspace '1'");
        Assert.Throws<DeploymentConflictException>(() => manager.AcquireLock("workspace:1", "workspace '1'"));
    }

    [Fact]
    public void AcquireLock_ManagerImplementsInterface()
    {
        IDeploymentLockManager manager = new DeploymentLockManager();
        using var handle = manager.AcquireLock(DeploymentLockKeys.Admin, "the tenant admin scope");
        Assert.NotNull(handle);
    }
}
