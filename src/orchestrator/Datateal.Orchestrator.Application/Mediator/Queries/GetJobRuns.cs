using Datateal.Core.Mediator;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Queries;

public record GetJobRunsRequest(Guid WorkspaceId, Guid JobId, int Limit = 20, int Offset = 0)
    : IRequest<IReadOnlyList<JobRun>>;

internal class GetJobRunsHandler(IJobRepository jobRepository, IJobRunRepository jobRunRepository)
    : IRequestHandler<GetJobRunsRequest, IReadOnlyList<JobRun>>
{
    public async Task<IReadOnlyList<JobRun>> Handle(GetJobRunsRequest request, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetJobAsync(request.JobId, cancellationToken);
        if (job is null || job.WorkspaceId != request.WorkspaceId) return [];

        return await jobRunRepository.GetJobRunsAsync(request.JobId, request.Limit, request.Offset, cancellationToken);
    }
}
