using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Application.Engine;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Commands;

public record CreateScheduleRequest(
    Guid JobId,
    string CronExpression,
    bool IsEnabled,
    string? TimeZone,
    Dictionary<string, string>? Parameters) : IRequest<JobSchedule>;

internal class CreateScheduleHandler(
    IScheduleRepository scheduleRepository,
    SchedulesManager schedulesManager)
    : IRequestHandler<CreateScheduleRequest, JobSchedule>
{
    public async Task<JobSchedule> Handle(CreateScheduleRequest request, CancellationToken cancellationToken)
    {
        var schedule = new JobSchedule
        {
            Id = Guid.NewGuid(),
            JobId = request.JobId,
            CronExpression = request.CronExpression,
            IsEnabled = request.IsEnabled,
            TimeZone = request.TimeZone,
            Parameters = request.Parameters,
        };

        var created = await scheduleRepository.CreateScheduleAsync(schedule, cancellationToken);
        await schedulesManager.AddScheduleAsync(created, cancellationToken);
        return created;
    }
}
