using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Queries;

public record GetCellOutputsRequest(Guid TaskRunId) : IRequest<IReadOnlyList<TaskRunCellOutput>>;

internal class GetCellOutputsHandler(IJobRunRepository jobRunRepository)
    : IRequestHandler<GetCellOutputsRequest, IReadOnlyList<TaskRunCellOutput>>
{
    public async Task<IReadOnlyList<TaskRunCellOutput>> Handle(GetCellOutputsRequest request, CancellationToken cancellationToken)
    {
        return await jobRunRepository.GetCellOutputsAsync(request.TaskRunId, cancellationToken);
    }
}
