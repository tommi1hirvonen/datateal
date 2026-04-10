using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Commands;

public record UpdateJobRequest(
    Guid Id,
    string Name,
    string? Description,
    Guid? FolderId,
    int MaxConcurrentRuns,
    bool IsEnabled) : IRequest<Job?>;

internal class UpdateJobHandler(IJobRepository jobRepository) : IRequestHandler<UpdateJobRequest, Job?>
{
    public async Task<Job?> Handle(UpdateJobRequest request, CancellationToken cancellationToken)
    {
        var job = new Job
        {
            Id = request.Id,
            Name = request.Name,
            Description = request.Description,
            FolderId = request.FolderId,
            MaxConcurrentRuns = request.MaxConcurrentRuns,
            IsEnabled = request.IsEnabled,
            UpdatedAt = DateTime.UtcNow,
        };

        return await jobRepository.UpdateJobAsync(job, cancellationToken);
    }
}
