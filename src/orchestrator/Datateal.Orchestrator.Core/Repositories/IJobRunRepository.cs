using Datateal.Core.Orchestration;
using Datateal.Orchestrator.Core.Entities;

namespace Datateal.Orchestrator.Core.Repositories;

public interface IJobRunRepository
{
    Task<JobRun?> GetJobRunAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobRun>> GetJobRunsAsync(Guid jobId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobRun>> GetAllRunsAsync(Guid workspaceId, string? jobName, string? status, DateTime? from, DateTime? to, int limit = 100, int offset = 0, CancellationToken cancellationToken = default);
    Task<JobRun> CreateJobRunAsync(JobRun run, CancellationToken cancellationToken = default);
    Task UpdateJobRunStatusAsync(Guid id, JobRunStatus status, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobRun>> GetActiveRunsAsync(CancellationToken cancellationToken = default);
    Task<int> GetActiveRunCountAsync(Guid jobId, CancellationToken cancellationToken = default);

    // Task runs
    Task<TaskRun?> GetTaskRunAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskRun> CreateTaskRunAsync(TaskRun taskRun, CancellationToken cancellationToken = default);
    Task UpdateTaskRunAsync(TaskRun taskRun, CancellationToken cancellationToken = default);

    // History retention
    Task<int> PurgeRunsOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
