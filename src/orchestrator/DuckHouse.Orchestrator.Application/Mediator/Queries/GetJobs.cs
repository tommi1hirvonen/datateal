using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Queries;

public record GetJobsRequest : IRequest<IReadOnlyList<Job>>;

internal class GetJobsHandler(IJobRepository jobRepository)
    : IRequestHandler<GetJobsRequest, IReadOnlyList<Job>>
{
    public async Task<IReadOnlyList<Job>> Handle(GetJobsRequest request, CancellationToken cancellationToken)
    {
        return await jobRepository.GetJobsAsync(cancellationToken);
    }
}
