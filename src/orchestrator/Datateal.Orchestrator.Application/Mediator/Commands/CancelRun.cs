using Datateal.Core.Mediator;
using Datateal.Core.Orchestration;
using Datateal.Orchestrator.Application.Engine;
using Datateal.Orchestrator.Core.Enums;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Commands;

public record CancelRunRequest(Guid WorkspaceId, Guid RunId) : IRequest;

internal class CancelRunHandler(
    IJobRunRepository jobRunRepository,
    RunDispatcher runDispatcher) : IRequestHandler<CancelRunRequest>
{
    public async Task Handle(CancelRunRequest request, CancellationToken cancellationToken)
    {
        var run = await jobRunRepository.GetJobRunAsync(request.RunId, cancellationToken)
            ?? throw new InvalidOperationException($"Run {request.RunId} not found.");
        if (run.WorkspaceId != request.WorkspaceId)
            throw new InvalidOperationException($"Run {request.RunId} not found.");

        // Cancel via the dispatcher (triggers CancellationToken on the coordinator)
        await runDispatcher.CancelRunAsync(run.Id);

        await jobRunRepository.UpdateJobRunStatusAsync(run.Id, JobRunStatus.Cancelled, cancellationToken);

        foreach (var taskRun in run.TaskRuns)
        {
            if (taskRun.Status is TaskRunStatus.Succeeded or TaskRunStatus.Failed)
                continue;

            taskRun.Status = TaskRunStatus.Cancelled;
            taskRun.CompletedAt = DateTime.UtcNow;
            await jobRunRepository.UpdateTaskRunAsync(taskRun, cancellationToken);
        }
    }
}
