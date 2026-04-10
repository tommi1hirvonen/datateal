using DuckHouse.Ui.Shared.Orchestration;

namespace DuckHouse.Ui.Client.Services;

public interface IJobService
{
    // Jobs
    Task<IReadOnlyList<JobSummary>> GetJobsAsync(CancellationToken ct = default);
    Task<JobDetail?> GetJobAsync(Guid id, CancellationToken ct = default);
    Task<JobSummary> CreateJobAsync(CreateJobRequest request, CancellationToken ct = default);
    Task<JobSummary?> UpdateJobAsync(Guid id, UpdateJobRequest request, CancellationToken ct = default);
    Task DeleteJobAsync(Guid id, CancellationToken ct = default);
    Task<JobRunSummary> TriggerJobAsync(Guid id, TriggerJobRequest? request = null, CancellationToken ct = default);

    // Runs
    Task<IReadOnlyList<JobRunSummary>> GetRunsAsync(Guid jobId, CancellationToken ct = default);
    Task<JobRunDetail?> GetRunAsync(Guid runId, CancellationToken ct = default);
    Task CancelRunAsync(Guid runId, CancellationToken ct = default);

    // Cell outputs
    Task<IReadOnlyList<CellOutputDto>> GetCellOutputsAsync(Guid runId, Guid taskRunId, CancellationToken ct = default);

    // Schedules
    Task<IReadOnlyList<ScheduleDto>> GetSchedulesAsync(Guid jobId, CancellationToken ct = default);
    Task<ScheduleDto> CreateScheduleAsync(Guid jobId, CreateScheduleRequest request, CancellationToken ct = default);
    Task<ScheduleDto?> UpdateScheduleAsync(Guid jobId, Guid scheduleId, UpdateScheduleRequest request, CancellationToken ct = default);
    Task DeleteScheduleAsync(Guid jobId, Guid scheduleId, CancellationToken ct = default);
}
