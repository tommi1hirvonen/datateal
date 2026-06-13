using Datateal.Core.Mediator;
using Datateal.Orchestrator.Core;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Queries;

public record GetJobsRequest : IRequest<IReadOnlyList<Job>>;

internal class GetJobsHandler(IJobRepository jobRepository, IWorkspaceContext workspace)
    : IRequestHandler<GetJobsRequest, IReadOnlyList<Job>>
{
    public async Task<IReadOnlyList<Job>> Handle(GetJobsRequest request, CancellationToken cancellationToken)
    {
        var jobs = await jobRepository.GetJobsAsync(cancellationToken);
        var workspaceId = workspace.CurrentWorkspaceId;
        return workspaceId is null ? jobs : jobs.Where(j => j.WorkspaceId == workspaceId).ToList();
    }
}
