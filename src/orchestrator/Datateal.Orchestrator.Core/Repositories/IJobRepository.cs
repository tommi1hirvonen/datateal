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
}
