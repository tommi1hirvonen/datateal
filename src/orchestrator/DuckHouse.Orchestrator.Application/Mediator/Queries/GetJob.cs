using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Queries;

public record GetJobRequest(Guid Id) : IRequest<Job?>;

internal class GetJobHandler(IJobRepository jobRepository)
    : IRequestHandler<GetJobRequest, Job?>
{
    public async Task<Job?> Handle(GetJobRequest request, CancellationToken cancellationToken)
    {
        return await jobRepository.GetJobAsync(request.Id, cancellationToken);
    }
}
