using Datateal.Core.Mediator;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Commands;

public record PlanJobDeploymentRequest(Guid WorkspaceId, Guid OwnerUserId, List<JobModel> Jobs) : IRequest<ChangeSet>;

public record ApplyJobDeploymentRequest(Guid WorkspaceId, Guid OwnerUserId, List<JobModel> Jobs) : IRequest<ChangeSet>;

internal sealed class PlanJobDeploymentHandler(
    IJobRepository jobRepository,
    JobModelMapper mapper) : IRequestHandler<PlanJobDeploymentRequest, ChangeSet>
{
    public async Task<ChangeSet> Handle(PlanJobDeploymentRequest request, CancellationToken cancellationToken)
    {
        var current = await JobDeploymentHelpers.LoadCurrentModelsAsync(request.WorkspaceId, jobRepository, mapper, cancellationToken);
        var diff = DiffEngine.Diff(mapper, JobDeploymentHelpers.NormalizeJobs(request.Jobs), current.Models, allowDeletes: true);

        foreach (var (model, _) in diff.Creations)
            _ = await mapper.ToCreateRequestAsync(request.WorkspaceId, request.OwnerUserId, model, cancellationToken);

        foreach (var (model, _) in diff.Updates)
            _ = await mapper.ToUpdateRequestAsync(
                request.WorkspaceId,
                current.ByName[mapper.NaturalKey(model)].Id,
                request.OwnerUserId,
                model,
                cancellationToken);

        return new ChangeSet
        {
            Scope = "workspace",
            Target = request.WorkspaceId.ToString(),
            DryRun = true,
            Changes = [.. diff.Changes],
        };
    }
}

internal sealed class ApplyJobDeploymentHandler(
    IJobRepository jobRepository,
    JobModelMapper mapper,
    IMediator mediator) : IRequestHandler<ApplyJobDeploymentRequest, ChangeSet>
{
    public async Task<ChangeSet> Handle(ApplyJobDeploymentRequest request, CancellationToken cancellationToken)
    {
        var current = await JobDeploymentHelpers.LoadCurrentModelsAsync(request.WorkspaceId, jobRepository, mapper, cancellationToken);
        var diff = DiffEngine.Diff(mapper, JobDeploymentHelpers.NormalizeJobs(request.Jobs), current.Models, allowDeletes: true);

        foreach (var (model, _) in diff.Creations)
        {
            var createRequest = await mapper.ToCreateRequestAsync(request.WorkspaceId, request.OwnerUserId, model, cancellationToken);
            await mediator.SendAsync(createRequest, cancellationToken);
        }

        foreach (var (model, _) in diff.Updates)
        {
            var currentJob = current.ByName[mapper.NaturalKey(model)];
            var updateRequest = await mapper.ToUpdateRequestAsync(
                request.WorkspaceId,
                currentJob.Id,
                request.OwnerUserId,
                model,
                cancellationToken);
            await mediator.SendAsync(updateRequest, cancellationToken);
        }

        foreach (var (model, _) in diff.Deletions)
        {
            var currentJob = current.ByName[mapper.NaturalKey(model)];
            await mediator.SendAsync(new DeleteJobRequest(request.WorkspaceId, currentJob.Id), cancellationToken);
        }

        return new ChangeSet
        {
            Scope = "workspace",
            Target = request.WorkspaceId.ToString(),
            DryRun = false,
            Changes = [.. diff.Changes],
        };
    }
}

internal static class JobDeploymentHelpers
{
    public static async Task<(List<JobModel> Models, Dictionary<string, Datateal.Orchestrator.Core.Entities.Job> ByName)> LoadCurrentModelsAsync(
        Guid workspaceId,
        IJobRepository repository,
        JobModelMapper mapper,
        CancellationToken cancellationToken)
    {
        var jobs = await repository.GetJobsAsync(workspaceId, cancellationToken);
        var detailedJobs = new List<Datateal.Orchestrator.Core.Entities.Job>();
        foreach (var job in jobs)
        {
            var detailed = await repository.GetJobDetailAsync(job.Id, cancellationToken);
            if (detailed is not null)
                detailedJobs.Add(detailed);
        }

        var models = new List<JobModel>();
        foreach (var job in detailedJobs)
            models.Add(await mapper.ToModelAsync(job, cancellationToken));

        return (models, detailedJobs.ToDictionary(job => job.Name, StringComparer.OrdinalIgnoreCase));
    }

    public static List<JobModel> NormalizeJobs(List<JobModel> jobs) =>
        jobs
            .OrderBy(job => job.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
