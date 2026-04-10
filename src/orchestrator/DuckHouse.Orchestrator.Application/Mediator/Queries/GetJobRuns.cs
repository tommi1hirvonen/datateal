using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Queries;

public record GetJobRunsRequest(Guid JobId, int Limit = 20, int Offset = 0)
    : IRequest<IReadOnlyList<JobRun>>;

internal class GetJobRunsHandler(IJobRunRepository jobRunRepository)
    : IRequestHandler<GetJobRunsRequest, IReadOnlyList<JobRun>>
{
    public async Task<IReadOnlyList<JobRun>> Handle(GetJobRunsRequest request, CancellationToken cancellationToken)
    {
        return await jobRunRepository.GetJobRunsAsync(request.JobId, request.Limit, request.Offset, cancellationToken);
    }
}
