using Datateal.Core.Mediator;
using Datateal.Orchestrator.Application.Yaml;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Commands;

public record ImportJobRequest(Guid WorkspaceId, string Yaml) : IRequest<Job>;

internal class ImportJobHandler(
    YamlJobImporter importer,
    IJobRepository jobRepository) : IRequestHandler<ImportJobRequest, Job>
{
    public async Task<Job> Handle(ImportJobRequest request, CancellationToken cancellationToken)
    {
        var job = await importer.ImportAsync(request.WorkspaceId, request.Yaml, cancellationToken);
        return await jobRepository.CreateJobAsync(job, cancellationToken);
    }
}
