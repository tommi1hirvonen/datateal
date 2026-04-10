using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Commands;

public record DeleteScheduleRequest(Guid Id) : IRequest;

internal class DeleteScheduleHandler(IScheduleRepository scheduleRepository)
    : IRequestHandler<DeleteScheduleRequest>
{
    public async Task Handle(DeleteScheduleRequest request, CancellationToken cancellationToken)
    {
        await scheduleRepository.DeleteScheduleAsync(request.Id, cancellationToken);
    }
}
