using System.Runtime.CompilerServices;
using Datateal.Data;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;
using Microsoft.EntityFrameworkCore;

[assembly: InternalsVisibleTo("Datateal.Core.Tests")]

namespace Datateal.Orchestrator.Infrastructure.Repositories;

internal class JobRepository(DatatealDbContext db) : IJobRepository
{
    public async Task<IReadOnlyList<Job>> GetJobsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
        await db.Jobs
            .Where(j => j.WorkspaceId == workspaceId)
            .Include(j => j.Parameters)
            .Include(j => j.Tasks)
            .Include(j => j.Schedules)
            .OrderBy(j => j.Name)
            .ToListAsync(cancellationToken);

    public async Task<Job?> GetJobAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Jobs
            .Include(j => j.Parameters)
            .Include(j => j.Tasks).ThenInclude(t => t.Dependencies)
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<Job?> GetJobDetailAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Jobs
            .Include(j => j.Parameters)
            .Include(j => j.Tasks).ThenInclude(t => t.Dependencies).ThenInclude(d => d.DependsOnTask)
            .Include(j => j.Schedules)
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<Job?> GetJobByNameAsync(string name, Guid workspaceId, CancellationToken cancellationToken = default) =>
        await db.Jobs
            .Include(j => j.Parameters)
            .Include(j => j.Tasks).ThenInclude(t => t.Dependencies)
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.WorkspaceId == workspaceId && j.Name == name, cancellationToken);

    public async Task<Job> CreateJobAsync(Job job, CancellationToken cancellationToken = default)
    {
        job.Id = Guid.CreateVersion7();
        job.CreatedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        foreach (var p in job.Parameters) p.Id = Guid.CreateVersion7();

        // Build the old→new mapping first so DependsOnTaskId cross-references can be
        // updated in the same pass. Reassigning t.Id without this causes the FK on
        // TaskDependencies.DependsOnTaskId to point at the old (now non-existent) task IDs.
        var oldToNew = job.Tasks.ToDictionary(t => t.Id, _ => Guid.CreateVersion7());
        foreach (var t in job.Tasks)
        {
            var newId = oldToNew[t.Id];
            t.Id = newId;
            foreach (var d in t.Dependencies)
            {
                d.Id = Guid.CreateVersion7();
                d.TaskId = newId;
                d.DependsOnTaskId = oldToNew[d.DependsOnTaskId];
            }
        }

        foreach (var s in job.Schedules) s.Id = Guid.CreateVersion7();

        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<Job?> UpdateJobAsync(Job job, CancellationToken cancellationToken = default)
    {
        // Capture the pre-update name via a fresh, untracked read (job is already tracked and
        // mutated in memory at this point, so a tracked query would return the new value —
        // AsNoTracking bypasses the identity map and hits the database, which still has the old row).
        var oldName = await db.Jobs
            .AsNoTracking()
            .Where(j => j.Id == job.Id)
            .Select(j => j.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var hasAmbientTransaction = db.Database.CurrentTransaction is not null;

        async Task ApplyAsync()
        {
            // The job entity is already tracked (loaded via GetJobAsync).
            // Collections were cleared and rebuilt in the handler — EF Core's change tracker
            // automatically marks orphaned children as Deleted for required cascade relationships,
            // so no explicit orphan cleanup is needed here.
            job.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            // Renaming a job leaves any other job's SubJobTask.SubJobName stale unless repointed
            // here — sub-job tasks reference jobs by name (not id) so the reference survives a
            // job's entity id staying the same across a rename, but the name itself must be kept
            // in sync everywhere it's referenced.
            if (oldName is not null && !string.Equals(oldName, job.Name, StringComparison.OrdinalIgnoreCase))
            {
                await db.JobTasks
                    .OfType<SubJobTask>()
                    .Where(t => t.Job!.WorkspaceId == job.WorkspaceId && t.SubJobName == oldName)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.SubJobName, job.Name), cancellationToken);
            }
        }

        if (hasAmbientTransaction)
        {
            // Called from within an outer ambient transaction (e.g. the orchestrator's job-apply
            // deployment loop): do not start a nested transaction or a new execution strategy —
            // Npgsql does not support true nested transactions, EF's retrying execution strategy
            // rejects being invoked while a user-initiated transaction is already open, and the
            // ambient transaction already provides the atomicity guarantee across the whole loop.
            await ApplyAsync();
        }
        else
        {
            // Standalone call (e.g. a direct PUT /jobs/{id} API call): keep the existing
            // self-contained transaction behavior.
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                await ApplyAsync();
                await transaction.CommitAsync(cancellationToken);
            });
        }

        return await GetJobAsync(job.Id, cancellationToken);
    }

    public async Task<bool> DeleteJobAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await db.Jobs.FindAsync([id], cancellationToken);
        if (job is null) return false;
        db.Jobs.Remove(job);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await action(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
