using DuckHouse.Core.Orchestration;
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
    Task<IReadOnlyList<JobRunSummary>> GetRunsAsync(Guid jobId, int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<JobRunSummary>> GetAllRunsAsync(string? jobName, JobRunStatus? status, DateTime? from, DateTime? to, int limit = 100, CancellationToken ct = default);
    Task<JobRunDetail?> GetRunAsync(Guid runId, CancellationToken ct = default);
    Task CancelRunAsync(Guid runId, CancellationToken ct = default);

    // Cell outputs
    Task<IReadOnlyList<CellOutputDto>> GetCellOutputsAsync(Guid runId, Guid taskRunId, CancellationToken ct = default);

    // Schedules
    Task<IReadOnlyList<ScheduleDto>> GetSchedulesAsync(Guid jobId, CancellationToken ct = default);
    Task<ScheduleDto> CreateScheduleAsync(Guid jobId, CreateScheduleRequest request, CancellationToken ct = default);
    Task<ScheduleDto?> UpdateScheduleAsync(Guid jobId, Guid scheduleId, UpdateScheduleRequest request, CancellationToken ct = default);
    Task DeleteScheduleAsync(Guid jobId, Guid scheduleId, CancellationToken ct = default);
    Task<IReadOnlyList<TimeZoneDto>> GetTimeZonesAsync(CancellationToken ct = default);

    // YAML import/export
    Task<string> ExportJobAsync(Guid id, CancellationToken ct = default);
    Task<JobSummary> ImportJobAsync(string yaml, CancellationToken ct = default);
}
