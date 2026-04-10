using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Queries;

public record GetJobRunRequest(Guid Id) : IRequest<JobRun?>;

internal class GetJobRunHandler(IJobRunRepository jobRunRepository)
    : IRequestHandler<GetJobRunRequest, JobRun?>
{
    public async Task<JobRun?> Handle(GetJobRunRequest request, CancellationToken cancellationToken)
    {
        return await jobRunRepository.GetJobRunAsync(request.Id, cancellationToken);
    }
}
