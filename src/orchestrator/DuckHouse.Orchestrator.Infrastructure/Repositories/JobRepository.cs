using DuckHouse.Data;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DuckHouse.Orchestrator.Infrastructure.Repositories;

internal class JobRepository(DuckHouseDbContext db) : IJobRepository
{
    public async Task<IReadOnlyList<Job>> GetJobsAsync(CancellationToken cancellationToken = default) =>
        await db.Jobs
            .Include(j => j.Parameters)
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

    public async Task<Job?> GetJobByNameAsync(string name, CancellationToken cancellationToken = default) =>
        await db.Jobs
            .Include(j => j.Parameters)
            .Include(j => j.Tasks).ThenInclude(t => t.Dependencies)
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Name == name, cancellationToken);

    public async Task<Job> CreateJobAsync(Job job, CancellationToken cancellationToken = default)
    {
        job.Id = Guid.CreateVersion7();
        job.CreatedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        foreach (var p in job.Parameters) p.Id = Guid.CreateVersion7();
        foreach (var t in job.Tasks)
        {
            t.Id = Guid.CreateVersion7();
            foreach (var d in t.Dependencies) d.Id = Guid.CreateVersion7();
        }
        foreach (var s in job.Schedules) s.Id = Guid.CreateVersion7();

        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<Job?> UpdateJobAsync(Job job, CancellationToken cancellationToken = default)
    {
        // The job entity is already tracked (loaded via GetJobAsync).
        // Collections were cleared and rebuilt in the handler — EF Core's change tracker
        // automatically marks orphaned children as Deleted for required cascade relationships,
        // so no explicit orphan cleanup is needed here.
        job.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

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
}
