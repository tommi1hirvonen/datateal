using System.Collections.Concurrent;
using Datateal.Core.Deployment;
using Datateal.Ui.Server.Core.Deployment;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

/// <inheritdoc cref="IDeploymentLockManager"/>
internal sealed class DeploymentLockManager : IDeploymentLockManager
{
    // One SemaphoreSlim(1,1) per lock key acts as a simple mutex; keys are created lazily and kept
    // around for the lifetime of the process (the set of distinct keys — workspace IDs plus the
    // single admin key — is small and bounded, so there is no meaningful memory growth concern).
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public IDisposable AcquireLock(string key, string displayName)
    {
        var semaphore = _locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));

        if (!semaphore.Wait(0))
        {
            throw new DeploymentConflictException(
                $"Another deployment is already in progress for {displayName}. " +
                "Wait for it to finish, then try again.");
        }

        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                semaphore.Release();
        }
    }
}
