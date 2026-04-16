using DuckHouse.Data;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DuckHouse.Orchestrator.Infrastructure.Repositories;

internal class ScheduleRepository(DuckHouseDbContext db) : IScheduleRepository
{
    public async Task<IReadOnlyList<JobSchedule>> GetEnabledSchedulesAsync(CancellationToken cancellationToken = default) =>
        await db.JobSchedules
            .Include(s => s.Job)
            .Where(s => s.IsEnabled && s.Job!.IsEnabled)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<JobSchedule>> GetSchedulesForJobAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        await db.JobSchedules
            .Where(s => s.JobId == jobId)
            .ToListAsync(cancellationToken);

    public async Task<JobSchedule?> GetScheduleAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.JobSchedules.FindAsync([id], cancellationToken);

    public async Task<JobSchedule> CreateScheduleAsync(JobSchedule schedule, CancellationToken cancellationToken = default)
    {
        schedule.Id = Guid.CreateVersion7();
        db.JobSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);
        return schedule;
    }

    public async Task<JobSchedule?> UpdateScheduleAsync(JobSchedule schedule, CancellationToken cancellationToken = default)
    {
        var existing = await db.JobSchedules.FindAsync([schedule.Id], cancellationToken);
        if (existing is null) return null;

        existing.CronExpression = schedule.CronExpression;
        existing.IsEnabled = schedule.IsEnabled;
        existing.TimeZone = schedule.TimeZone;
        existing.Parameters = schedule.Parameters;
        existing.NextFireTime = schedule.NextFireTime;

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteScheduleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var schedule = await db.JobSchedules.FindAsync([id], cancellationToken);
        if (schedule is null) return false;
        db.JobSchedules.Remove(schedule);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateNextFireTimeAsync(Guid id, DateTime? nextFireTime, CancellationToken cancellationToken = default)
    {
        var schedule = await db.JobSchedules.FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException($"Schedule {id} not found.");
        schedule.NextFireTime = nextFireTime;
        await db.SaveChangesAsync(cancellationToken);
    }
}
