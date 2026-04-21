using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Application.Engine;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Commands;

public record DeleteScheduleRequest(Guid Id) : IRequest;

internal class DeleteScheduleHandler(
    IScheduleRepository scheduleRepository,
    SchedulesManager schedulesManager)
    : IRequestHandler<DeleteScheduleRequest>
{
    public async Task Handle(DeleteScheduleRequest request, CancellationToken cancellationToken)
    {
        var schedule = await scheduleRepository.GetScheduleAsync(request.Id, cancellationToken);
        await scheduleRepository.DeleteScheduleAsync(request.Id, cancellationToken);
        if (schedule is not null)
        {
            await schedulesManager.RemoveScheduleAsync(schedule, cancellationToken);
        }
    }
}
