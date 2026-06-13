using Datateal.Core.Mediator;
using Datateal.Orchestrator.Application.Engine;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Commands;

public record DeleteJobRequest(Guid WorkspaceId, Guid Id) : IRequest<bool>;

internal class DeleteJobHandler(
    IJobRepository jobRepository,
    SchedulesManager schedulesManager)
    : IRequestHandler<DeleteJobRequest, bool>
{
    public async Task<bool> Handle(DeleteJobRequest request, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetJobAsync(request.Id, cancellationToken);
        if (job is null || job.WorkspaceId != request.WorkspaceId) return false;

        await jobRepository.DeleteJobAsync(request.Id, cancellationToken);
        await schedulesManager.RemoveJobAsync(request.Id, cancellationToken);
        return true;
    }
}
