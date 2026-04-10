using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Queries;

public record GetTaskRunRequest(Guid Id) : IRequest<TaskRun?>;

internal class GetTaskRunHandler(IJobRunRepository jobRunRepository)
    : IRequestHandler<GetTaskRunRequest, TaskRun?>
{
    public async Task<TaskRun?> Handle(GetTaskRunRequest request, CancellationToken cancellationToken)
    {
        return await jobRunRepository.GetTaskRunAsync(request.Id, cancellationToken);
    }
}
