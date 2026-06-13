using Datateal.Core.Mediator;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Queries;

public record GetJobsRequest(Guid WorkspaceId) : IRequest<IReadOnlyList<Job>>;

internal class GetJobsHandler(IJobRepository jobRepository)
    : IRequestHandler<GetJobsRequest, IReadOnlyList<Job>>
{
    public async Task<IReadOnlyList<Job>> Handle(GetJobsRequest request, CancellationToken cancellationToken)
    {
        var jobs = await jobRepository.GetJobsAsync(cancellationToken);
        return jobs.Where(j => j.WorkspaceId == request.WorkspaceId).ToList();
    }
}
