using Datateal.Orchestrator.Core.Entities;

namespace Datateal.Orchestrator.Core.Repositories;

public interface IJobRepository
{
    Task<IReadOnlyList<Job>> GetJobsAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Job?> GetJobAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Job?> GetJobDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Job?> GetJobByNameAsync(string name, Guid workspaceId, CancellationToken cancellationToken = default);
    Task<Job> CreateJobAsync(Job job, CancellationToken cancellationToken = default);
    Task<Job?> UpdateJobAsync(Job job, CancellationToken cancellationToken = default);
    Task<bool> DeleteJobAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="action"/> inside a single ambient database transaction (using EF
    /// Core's retrying execution strategy), so that any number of job creates/updates/deletes
    /// performed by nested repository/mediator calls either all commit together or are all
    /// rolled back together. If <paramref name="action"/> throws, the transaction is rolled back
    /// and the exception is rethrown to the caller.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}
