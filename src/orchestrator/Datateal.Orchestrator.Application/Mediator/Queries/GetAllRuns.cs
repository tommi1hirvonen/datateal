using Datateal.Core.Mediator;
using Datateal.Orchestrator.Core;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Queries;

public record GetAllRunsRequest(
    string? JobName,
    string? Status,
    DateTime? From,
    DateTime? To,
    int Limit = 100,
    int Offset = 0) : IRequest<IReadOnlyList<JobRun>>;

internal class GetAllRunsHandler(IJobRunRepository jobRunRepository, IWorkspaceContext workspace)
    : IRequestHandler<GetAllRunsRequest, IReadOnlyList<JobRun>>
{
    public async Task<IReadOnlyList<JobRun>> Handle(GetAllRunsRequest request, CancellationToken cancellationToken)
    {
        var runs = await jobRunRepository.GetAllRunsAsync(
            request.JobName, request.Status, request.From, request.To,
            request.Limit, request.Offset, cancellationToken);
        var workspaceId = workspace.CurrentWorkspaceId;
        return workspaceId is null ? runs : runs.Where(r => r.WorkspaceId == workspaceId).ToList();
    }
}
