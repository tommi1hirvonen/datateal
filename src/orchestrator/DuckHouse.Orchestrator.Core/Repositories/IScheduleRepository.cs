using DuckHouse.Orchestrator.Core.Entities;

namespace DuckHouse.Orchestrator.Core.Repositories;

public interface IScheduleRepository
{
    Task<IReadOnlyList<JobSchedule>> GetAllSchedulesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobSchedule>> GetSchedulesForJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<JobSchedule?> GetScheduleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<JobSchedule> CreateScheduleAsync(JobSchedule schedule, CancellationToken cancellationToken = default);
    Task<JobSchedule?> UpdateScheduleAsync(JobSchedule schedule, CancellationToken cancellationToken = default);
    Task<bool> DeleteScheduleAsync(Guid id, CancellationToken cancellationToken = default);
}
