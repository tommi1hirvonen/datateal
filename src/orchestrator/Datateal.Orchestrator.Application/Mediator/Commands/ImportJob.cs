using Datateal.Core.Mediator;
using Datateal.Orchestrator.Application.Yaml;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Commands;

public record ImportJobRequest(Guid WorkspaceId, string Yaml, Guid? OwnerUserId) : IRequest<Job>;

internal class ImportJobHandler(
    YamlJobImporter importer,
    IJobRepository jobRepository) : IRequestHandler<ImportJobRequest, Job>
{
    public async Task<Job> Handle(ImportJobRequest request, CancellationToken cancellationToken)
    {
        if (request.OwnerUserId is null)
            throw new InvalidOperationException(JobOwner.MissingOwnerMessage);

        var job = await importer.ImportAsync(request.WorkspaceId, request.Yaml, cancellationToken);
        job.OwnerUserId = request.OwnerUserId;
        job.CreatedByUserId = request.OwnerUserId;
        return await jobRepository.CreateJobAsync(job, cancellationToken);
    }
}
