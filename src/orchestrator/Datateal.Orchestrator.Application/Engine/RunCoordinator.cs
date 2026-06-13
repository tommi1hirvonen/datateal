using System.Text.Json;
using System.Text.Json.Serialization;
using Datateal.Core.Mediator;
using Datateal.Core.Orchestration;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Enums;
using Datateal.Orchestrator.Core.Interfaces;
using Datateal.Orchestrator.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Datateal.Orchestrator.Application.Engine;

/// <summary>
/// Drives a single job run through its DAG: evaluates dependencies,
/// dispatches ready tasks, handles completions, and determines the final outcome.
/// </summary>
public class RunCoordinator(
    IJobRepository jobRepository,
    IJobRunRepository jobRunRepository,
    INodePoolConfigRepository nodePoolConfigRepo,
    IControlPlaneClient controlPlane,
    IWheelPackageReader wheelPackageReader,
    IEnvironmentResolver environmentResolver,
    IMediator mediator,
    WarmPoolManager warmPoolManager,
    IServiceScopeFactory scopeFactory,
    ILoggerFactory loggerFactory)
{
    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    private readonly ILogger<RunCoordinator> _logger = loggerFactory.CreateLogger<RunCoordinator>();

    public async Task ExecuteRunAsync(Guid jobRunId, CancellationToken ct)
    {
        var run = await jobRunRepository.GetJobRunAsync(jobRunId, ct)
            ?? throw new InvalidOperationException($"Job run {jobRunId} not found.");

        var job = ResolveJob(run)
            ?? await LoadJobFromDb(run, ct);

        _logger.LogInformation("Starting job run {RunId} for job '{JobName}'", jobRunId, job.Name);

        run.Status = JobRunStatus.Running;
        run.StartedAt = DateTime.UtcNow;
        await jobRunRepository.UpdateJobRunStatusAsync(run.Id, JobRunStatus.Running, ct);

        var nodeManager = new NodeManager(
            controlPlane, nodePoolConfigRepo, wheelPackageReader, environmentResolver, jobRunId,
            run.WorkspaceId,
            warmPoolManager,
            loggerFactory.CreateLogger<NodeManager>());

        var taskExecutor = new TaskExecutor(
            controlPlane, scopeFactory, mediator,
            loggerFactory.CreateLogger<TaskExecutor>());

        // Build lookup maps
        var taskMap = job.Tasks.ToDictionary(t => t.Id);
        var taskRunMap = run.TaskRuns
            .Where(tr => tr.TaskId.HasValue)
            .ToDictionary(tr => tr.TaskId!.Value);

        try
        {
            await RunDagAsync(run, job, taskMap, taskRunMap, taskExecutor, nodeManager, ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Job run {RunId} was cancelled", jobRunId);
            await CancelRemainingTasks(taskRunMap, ct);
            run.Status = JobRunStatus.Cancelled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in job run {RunId}", jobRunId);
            run.Status = JobRunStatus.Failed;
        }
        finally
        {
            // Clean up all provisioned nodes
            try
            {
                await nodeManager.CleanupAllAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up nodes for run {RunId}", jobRunId);
            }

            // Determine final outcome if not already set
            if (run.Status == JobRunStatus.Running)
                run.Status = DetermineOutcome(taskRunMap);

            run.CompletedAt = DateTime.UtcNow;
            await jobRunRepository.UpdateJobRunStatusAsync(run.Id, run.Status, CancellationToken.None);
            _logger.LogInformation("Job run {RunId} completed with status {Status}",
                jobRunId, run.Status);
        }
    }

    private async Task RunDagAsync(
        JobRun run,
        Job job,
        Dictionary<Guid, JobTask> taskMap,
        Dictionary<Guid, TaskRun> taskRunMap,
        TaskExecutor taskExecutor,
        NodeManager nodeManager,
        CancellationToken ct)
    {
        var activeTasks = new Dictionary<Guid, Task>(); // taskId → executing Task

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            // Propagate skips
            PropagateSkips(job.Tasks, taskRunMap);

            // Find ready tasks
            var readyTasks = FindReadyTasks(job.Tasks, taskRunMap);

            // Dispatch ready tasks
            foreach (var task in readyTasks)
            {
                if (activeTasks.ContainsKey(task.Id)) continue;

                var taskRun = taskRunMap[task.Id];
                taskRun.Status = TaskRunStatus.Running;
                taskRun.StartedAt = DateTime.UtcNow;
                await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);

                _logger.LogInformation("Dispatching task '{TaskName}' (attempt {Attempt})",
                    task.Name, taskRun.AttemptNumber);

                var capturedTask = task;
                var capturedTaskRun = taskRun;
                activeTasks[task.Id] = Task.Run(async () =>
                {
                    try
                    {
                        await taskExecutor.ExecuteAsync(capturedTaskRun, capturedTask, nodeManager, run.Parameters, ct);
                        capturedTaskRun.Status = TaskRunStatus.Succeeded;
                    }
                    catch (OperationCanceledException)
                    {
                        capturedTaskRun.Status = TaskRunStatus.Cancelled;
                    }
                    catch (Exception ex)
                    {
                        capturedTaskRun.ErrorMessage = ex.Message;

                        // Check retry
                        if (capturedTaskRun.AttemptNumber <= capturedTask.MaxRetries)
                        {
                            capturedTaskRun.Status = TaskRunStatus.Retrying;
                            _logger.LogWarning(
                                "Task '{TaskName}' failed (attempt {Attempt}/{MaxAttempts}), will retry in {Interval}",
                                capturedTask.Name, capturedTaskRun.AttemptNumber,
                                capturedTask.MaxRetries + 1, capturedTask.RetryInterval);
                        }
                        else
                        {
                            capturedTaskRun.Status = TaskRunStatus.Failed;
                            _logger.LogError(ex, "Task '{TaskName}' failed after {Attempts} attempt(s)",
                                capturedTask.Name, capturedTaskRun.AttemptNumber);
                        }
                    }
                    finally
                    {
                        capturedTaskRun.CompletedAt = DateTime.UtcNow;
                        if (capturedTaskRun.StartedAt.HasValue)
                            capturedTaskRun.DurationMs = (capturedTaskRun.CompletedAt.Value - capturedTaskRun.StartedAt.Value).TotalMilliseconds;
                    }
                }, ct);
            }

            if (activeTasks.Count == 0 && readyTasks.Count == 0)
                break; // No more tasks to dispatch or wait for

            if (activeTasks.Count == 0)
                break; // Shouldn't happen, but guard against infinite loop

            // Wait for any task to complete
            var completed = await Task.WhenAny(activeTasks.Values);

            // Find and remove completed tasks
            var completedTaskIds = activeTasks
                .Where(kv => kv.Value.IsCompleted)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var taskId in completedTaskIds)
            {
                activeTasks.Remove(taskId);
                var taskRun = taskRunMap[taskId];
                await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);

                // Handle retry: reset for next attempt
                if (taskRun.Status == TaskRunStatus.Retrying)
                {
                    var task = taskMap[taskId];
                    await Task.Delay(task.RetryInterval, ct);
                    taskRun.AttemptNumber++;
                    taskRun.Status = TaskRunStatus.Pending;
                    taskRun.StartedAt = null;
                    taskRun.CompletedAt = null;
                    taskRun.DurationMs = null;
                    taskRun.ErrorMessage = null;
                    await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);
                }
            }
        }

        // Wait for any remaining active tasks
        if (activeTasks.Count > 0)
            await Task.WhenAll(activeTasks.Values);
    }

    /// <summary>
    /// Finds tasks whose dependencies are all satisfied and are ready to execute.
    /// </summary>
    private static List<JobTask> FindReadyTasks(
        List<JobTask> tasks,
        Dictionary<Guid, TaskRun> taskRunMap)
    {
        var ready = new List<JobTask>();

        foreach (var task in tasks)
        {
            var taskRun = taskRunMap[task.Id];
            if (taskRun.Status != TaskRunStatus.Pending)
                continue;

            if (task.Dependencies.Count == 0)
            {
                ready.Add(task);
                continue;
            }

            var allSatisfied = task.Dependencies.All(dep =>
            {
                var upstreamRun = taskRunMap[dep.DependsOnTaskId];
                return IsDependencySatisfied(dep.Condition, upstreamRun.Status);
            });

            if (allSatisfied)
                ready.Add(task);
        }

        return ready;
    }

    /// <summary>
    /// Checks if a dependency condition is satisfied by the upstream task's status.
    /// </summary>
    private static bool IsDependencySatisfied(DependencyCondition condition, TaskRunStatus upstreamStatus)
    {
        return condition switch
        {
            DependencyCondition.OnSuccess => upstreamStatus == TaskRunStatus.Succeeded,
            DependencyCondition.OnFailure => upstreamStatus == TaskRunStatus.Failed,
            DependencyCondition.OnSkip => upstreamStatus == TaskRunStatus.Skipped,
            DependencyCondition.OnCompletion => upstreamStatus
                is TaskRunStatus.Succeeded or TaskRunStatus.Failed
                or TaskRunStatus.Skipped or TaskRunStatus.Cancelled,
            _ => false,
        };
    }

    /// <summary>
    /// Propagates skip status to tasks whose dependencies can never be satisfied.
    /// </summary>
    private static void PropagateSkips(
        List<JobTask> tasks,
        Dictionary<Guid, TaskRun> taskRunMap)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var task in tasks)
            {
                var taskRun = taskRunMap[task.Id];
                if (taskRun.Status != TaskRunStatus.Pending || task.Dependencies.Count == 0)
                    continue;

                var shouldSkip = task.Dependencies.Any(dep =>
                {
                    var upstreamRun = taskRunMap[dep.DependsOnTaskId];
                    return CanNeverBeSatisfied(dep.Condition, upstreamRun.Status);
                });

                if (shouldSkip)
                {
                    taskRun.Status = TaskRunStatus.Skipped;
                    taskRun.CompletedAt = DateTime.UtcNow;
                    changed = true;
                }
            }
        }
    }

    /// <summary>
    /// Determines if a dependency condition can NEVER be satisfied given the upstream's terminal status.
    /// </summary>
    private static bool CanNeverBeSatisfied(DependencyCondition condition, TaskRunStatus upstreamStatus)
    {
        if (!IsTerminal(upstreamStatus))
            return false;

        return condition switch
        {
            DependencyCondition.OnSuccess => upstreamStatus != TaskRunStatus.Succeeded,
            DependencyCondition.OnFailure => upstreamStatus != TaskRunStatus.Failed,
            DependencyCondition.OnSkip => upstreamStatus != TaskRunStatus.Skipped,
            DependencyCondition.OnCompletion => false, // Always satisfiable once terminal
            _ => false,
        };
    }

    private static bool IsTerminal(TaskRunStatus status) =>
        status is TaskRunStatus.Succeeded or TaskRunStatus.Failed
            or TaskRunStatus.Skipped or TaskRunStatus.Cancelled;

    /// <summary>
    /// Determines the final outcome of the job run based on task statuses.
    /// </summary>
    private static JobRunStatus DetermineOutcome(Dictionary<Guid, TaskRun> taskRunMap)
    {
        var statuses = taskRunMap.Values.Select(tr => tr.Status).ToList();

        if (statuses.All(s => s is TaskRunStatus.Succeeded or TaskRunStatus.Skipped))
            return JobRunStatus.Succeeded;

        if (statuses.Any(s => s == TaskRunStatus.Cancelled))
            return JobRunStatus.Cancelled;

        return JobRunStatus.Failed;
    }

    private async Task CancelRemainingTasks(
        Dictionary<Guid, TaskRun> taskRunMap, CancellationToken ct)
    {
        foreach (var taskRun in taskRunMap.Values)
        {
            if (IsTerminal(taskRun.Status)) continue;
            taskRun.Status = TaskRunStatus.Cancelled;
            taskRun.CompletedAt = DateTime.UtcNow;
            await jobRunRepository.UpdateTaskRunAsync(taskRun, CancellationToken.None);
        }
    }

    /// <summary>
    /// Deserializes the job snapshot stored in the run, if present.
    /// Returns null if the run has no snapshot (backward compatibility with pre-existing runs).
    /// </summary>
    private Job? ResolveJob(JobRun run)
    {
        if (run.SnapshotJson is null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<Job>(run.SnapshotJson, SnapshotOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize job snapshot for run {RunId}; falling back to live DB", run.Id);
            return null;
        }
    }

    /// <summary>
    /// Loads the job from the live database. Used only as a fallback for runs
    /// that pre-date the snapshot feature.
    /// </summary>
    private async Task<Job> LoadJobFromDb(JobRun run, CancellationToken ct)
    {
        _logger.LogWarning(
            "Job run {RunId} has no snapshot; loading live job from DB (run may have been created before snapshot support was added)",
            run.Id);

        return await jobRepository.GetJobAsync(
                run.JobId ?? throw new InvalidOperationException(
                    $"Job run {run.Id} has no snapshot and no associated job (job was deleted)."), ct)
            ?? throw new InvalidOperationException($"Job {run.JobId} not found.");
    }
}
