using DuckHouse.Data;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Enums;
using DuckHouse.Orchestrator.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DuckHouse.Orchestrator.Infrastructure.Repositories;

internal class JobRunRepository(DuckHouseDbContext db) : IJobRunRepository
{
    public async Task<JobRun?> GetJobRunAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.JobRuns
            .Include(r => r.TaskRuns)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<JobRun>> GetJobRunsAsync(Guid jobId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default) =>
        await db.JobRuns
            .Where(r => r.JobId == jobId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<JobRun> CreateJobRunAsync(JobRun run, CancellationToken cancellationToken = default)
    {
        run.Id = Guid.CreateVersion7();
        run.CreatedAt = DateTime.UtcNow;
        db.JobRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return run;
    }

    public async Task UpdateJobRunStatusAsync(Guid id, JobRunStatus status, CancellationToken cancellationToken = default)
    {
        var run = await db.JobRuns.FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException($"JobRun {id} not found.");
        run.Status = status;
        if (status == JobRunStatus.Running && run.StartedAt is null)
            run.StartedAt = DateTime.UtcNow;
        if (status is JobRunStatus.Succeeded or JobRunStatus.Failed or JobRunStatus.Cancelled)
            run.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobRun>> GetActiveRunsAsync(CancellationToken cancellationToken = default) =>
        await db.JobRuns
            .Include(r => r.TaskRuns)
            .Where(r => r.Status == JobRunStatus.Pending || r.Status == JobRunStatus.Running)
            .ToListAsync(cancellationToken);

    public async Task<int> GetActiveRunCountAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        await db.JobRuns
            .CountAsync(r => r.JobId == jobId && (r.Status == JobRunStatus.Pending || r.Status == JobRunStatus.Running), cancellationToken);

    public async Task<TaskRun?> GetTaskRunAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.TaskRuns
            .FirstOrDefaultAsync(tr => tr.Id == id, cancellationToken);

    public async Task<TaskRun> CreateTaskRunAsync(TaskRun taskRun, CancellationToken cancellationToken = default)
    {
        taskRun.Id = Guid.CreateVersion7();
        db.TaskRuns.Add(taskRun);
        await db.SaveChangesAsync(cancellationToken);
        return taskRun;
    }

    public async Task UpdateTaskRunAsync(TaskRun taskRun, CancellationToken cancellationToken = default)
    {
        db.TaskRuns.Update(taskRun);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> PurgeRunsOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default) =>
        await db.JobRuns
            .Where(r => r.CompletedAt != null && r.CompletedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
}
