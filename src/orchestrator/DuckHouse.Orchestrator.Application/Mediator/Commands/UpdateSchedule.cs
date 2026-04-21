using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Application.Engine;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Commands;

public record UpdateScheduleRequest(
    Guid Id,
    string CronExpression,
    bool IsEnabled,
    string? TimeZone,
    Dictionary<string, string>? Parameters) : IRequest<JobSchedule?>;

internal class UpdateScheduleHandler(
    IScheduleRepository scheduleRepository,
    SchedulesManager schedulesManager)
    : IRequestHandler<UpdateScheduleRequest, JobSchedule?>
{
    public async Task<JobSchedule?> Handle(UpdateScheduleRequest request, CancellationToken cancellationToken)
    {
        var schedule = new JobSchedule
        {
            Id = request.Id,
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
