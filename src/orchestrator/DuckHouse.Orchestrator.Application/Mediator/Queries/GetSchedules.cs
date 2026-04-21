using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Application.Engine;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Queries;

public record GetSchedulesRequest(Guid JobId) : IRequest<IReadOnlyList<JobSchedule>>;

internal class GetSchedulesHandler(
    IScheduleRepository scheduleRepository,
    SchedulesManager schedulesManager)
    : IRequestHandler<GetSchedulesRequest, IReadOnlyList<JobSchedule>>
{
    public async Task<IReadOnlyList<JobSchedule>> Handle(GetSchedulesRequest request, CancellationToken cancellationToken)
    {
        var schedules = await scheduleRepository.GetSchedulesForJobAsync(request.JobId, cancellationToken);
        foreach (var schedule in schedules)
        {
            schedule.NextFireTime = await schedulesManager.GetNextFireTimeAsync(schedule.Id, cancellationToken);
        }
        return schedules;
    }
}
