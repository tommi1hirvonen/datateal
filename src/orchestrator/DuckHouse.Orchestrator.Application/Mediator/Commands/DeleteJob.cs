using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Application.Engine;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Commands;

public record DeleteJobRequest(Guid Id) : IRequest;

internal class DeleteJobHandler(
    IJobRepository jobRepository,
    SchedulesManager schedulesManager)
    : IRequestHandler<DeleteJobRequest>
{
    public async Task Handle(DeleteJobRequest request, CancellationToken cancellationToken)
    {
        await jobRepository.DeleteJobAsync(request.Id, cancellationToken);
        await schedulesManager.RemoveJobAsync(request.Id, cancellationToken);
    }
}
