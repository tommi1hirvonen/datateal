using System.Text.Json;
using System.Text.Json.Serialization;
using Datateal.Core.Mediator;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Queries;

public record GetJobRunRequest(Guid WorkspaceId, Guid Id) : IRequest<JobRun?>;

internal class GetJobRunHandler(IJobRunRepository jobRunRepository)
    : IRequestHandler<GetJobRunRequest, JobRun?>
{
    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    private record SnapshotDependency(Guid DependsOnTaskId);
    private record SnapshotTask(Guid Id, string Name, List<SnapshotDependency>? Dependencies);
    private record JobSnapshot(List<SnapshotTask>? Tasks);

    public async Task<JobRun?> Handle(GetJobRunRequest request, CancellationToken cancellationToken)
    {
        var run = await jobRunRepository.GetJobRunAsync(request.Id, cancellationToken);
        if (run is null || run.WorkspaceId != request.WorkspaceId) return null;

        EnrichWithSnapshotDependencies(run);
        return run;
    }

    private static void EnrichWithSnapshotDependencies(JobRun run)
    {
        if (run.SnapshotJson is null) return;
        try
        {
            var snapshot = JsonSerializer.Deserialize<JobSnapshot>(run.SnapshotJson, SnapshotOptions);
            if (snapshot?.Tasks is null) return;

            var nameById = snapshot.Tasks.ToDictionary(t => t.Id, t => t.Name);
            var depsByTaskId = snapshot.Tasks.ToDictionary(
                t => t.Id,
                t => t.Dependencies?
                    .Select(d => nameById.GetValueOrDefault(d.DependsOnTaskId))
                    .OfType<string>()
                    .ToList() ?? []);

            foreach (var taskRun in run.TaskRuns)
            {
                if (taskRun.TaskId is { } taskId && depsByTaskId.TryGetValue(taskId, out var deps))
                    taskRun.DependencyTaskNames = deps;
            }
        }
        catch (JsonException) { /* snapshot unreadable; dependency names stay empty */ }
    }
}
