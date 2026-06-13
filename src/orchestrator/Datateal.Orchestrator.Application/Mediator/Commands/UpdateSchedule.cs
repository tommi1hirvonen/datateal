using Datateal.Core.Mediator;
using Datateal.Orchestrator.Application.Engine;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Commands;

public record UpdateScheduleRequest(
    Guid WorkspaceId,
    Guid Id,
    string CronExpression,
    bool IsEnabled,
    string? TimeZone,
    Dictionary<string, string>? Parameters) : IRequest<JobSchedule?>;

internal class UpdateScheduleHandler(
    IJobRepository jobRepository,
    IScheduleRepository scheduleRepository,
    SchedulesManager schedulesManager)
    : IRequestHandler<UpdateScheduleRequest, JobSchedule?>
{
    public async Task<JobSchedule?> Handle(UpdateScheduleRequest request, CancellationToken cancellationToken)
    {
        var existing = await scheduleRepository.GetScheduleAsync(request.Id, cancellationToken);
        if (existing is null) return null;

        var job = await jobRepository.GetJobAsync(existing.JobId, cancellationToken);
        if (job is null || job.WorkspaceId != request.WorkspaceId) return null;

        var schedule = new JobSchedule
        {
            Id = request.Id,
            JobId = existing.JobId,
            CronExpression = request.CronExpression,
            IsEnabled = request.IsEnabled,
            TimeZone = request.TimeZone,
            Parameters = request.Parameters,
        };

        var updated = await scheduleRepository.UpdateScheduleAsync(schedule, cancellationToken);
        if (updated is not null)
        {
            await schedulesManager.UpdateScheduleAsync(updated, cancellationToken);
        }
        return updated;
    }
}
