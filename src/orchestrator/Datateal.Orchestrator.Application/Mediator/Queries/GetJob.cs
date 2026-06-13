using Datateal.Core.Mediator;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Queries;

public record GetJobRequest(Guid WorkspaceId, Guid Id) : IRequest<Job?>;

internal class GetJobHandler(IJobRepository jobRepository)
    : IRequestHandler<GetJobRequest, Job?>
{
    public async Task<Job?> Handle(GetJobRequest request, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetJobDetailAsync(request.Id, cancellationToken);
        return job?.WorkspaceId == request.WorkspaceId ? job : null;
    }
}
