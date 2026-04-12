using System.Text.Json;
using DuckHouse.Core.Kernels;
using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Application.Mediator.Commands;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Enums;
using DuckHouse.Orchestrator.Core.Interfaces;
using DuckHouse.Orchestrator.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace DuckHouse.Orchestrator.Application.Engine;

/// <summary>
/// Executes individual tasks based on their type (Notebook, SQL, SubJob).
/// </summary>
public class TaskExecutor(
    IControlPlaneClient controlPlane,
    IWorkspaceReader workspaceReader,
    IJobRunRepository jobRunRepository,
    IMediator mediator,
    ILogger<TaskExecutor> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ExecuteAsync(TaskRun taskRun, JobTask task, NodeManager nodeManager, CancellationToken ct)
    {
        switch (task)
        {
            case NotebookTask notebook:
                await ExecuteNotebookAsync(taskRun, notebook, nodeManager, ct);
                break;
            case SqlQueryTask sqlQuery:
                await ExecuteSqlQueryAsync(taskRun, sqlQuery, nodeManager, ct);
                break;
            case SubJobTask subJob:
                await ExecuteSubJobAsync(taskRun, subJob, ct);
                break;
            default:
                throw new InvalidOperationException($"Unknown task type: {task.GetType().Name}");
        }
    }

    // ── Notebook execution ──────────────────────────────────────────

    private async Task ExecuteNotebookAsync(
        TaskRun taskRun, NotebookTask task, NodeManager nodeManager, CancellationToken ct)
    {
        var content = await workspaceReader.GetNotebookContentAsync(task.NotebookId, ct)
            ?? throw new InvalidOperationException(
                $"Notebook {task.NotebookId} not found in workspace.");

        var cells = ParseNotebookCells(content.Content);
        logger.LogInformation("Executing notebook '{Title}' with {Count} code cells",
            content.Title, cells.Count);

        // Create cell output records
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            await jobRunRepository.CreateCellOutputAsync(new TaskRunCellOutput
            {
                Id = Guid.CreateVersion7(),
                TaskRunId = taskRun.Id,
                CellIndex = i,
                CellSource = cell.Source,
                CellType = cell.Type,
                Language = cell.Type == "Markdown" ? null : cell.Language,
                Status = CellExecutionStatus.Pending,
            }, ct);
        }

        // Provision node and kernel
        string? nodeName = null;
        string? kernelId = null;

        if (task.NodePoolRef is not null)
        {
            (nodeName, kernelId) = await nodeManager.CreateKernelAsync(task.NodePoolRef, ct);
            taskRun.NodeName = nodeName;
            taskRun.KernelId = kernelId;
            await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);
        }
        else
        {
            throw new InvalidOperationException(
                $"Notebook task '{task.Name}' requires a NodePoolRef to execute.");
        }

        try
        {
            for (var i = 0; i < cells.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var cell = cells[i];
                if (cell.Type != "Code")
                    continue;

                var cellOutputs = await jobRunRepository.GetCellOutputsAsync(taskRun.Id, ct);
                var cellOutput = cellOutputs.First(c => c.CellIndex == i);

                cellOutput.Status = CellExecutionStatus.Running;
                cellOutput.StartedAt = DateTime.UtcNow;
                await jobRunRepository.UpdateCellOutputAsync(cellOutput, ct);

                // Wrap SQL cells
                var code = cell.Language == "Sql"
                    ? $"import duckdb; duckdb.sql(\"\"\"{cell.Source}\"\"\")"
                    : cell.Source;

                try
                {
                    var result = await ExecuteCodeAsync(nodeName, kernelId, code, ct);

                    cellOutput.Status = CellExecutionStatus.Succeeded;
                    cellOutput.CompletedAt = DateTime.UtcNow;
                    cellOutput.DurationMs = result.DurationMs;
                    cellOutput.ExecutionCount = result.ExecutionCount;
                    cellOutput.OutputsJson = JsonSerializer.Serialize(result.Outputs, JsonOptions);
                    if (result.Error is not null)
                    {
                        cellOutput.Status = CellExecutionStatus.Failed;
                        cellOutput.ErrorJson = JsonSerializer.Serialize(result.Error, JsonOptions);
                    }
                    await jobRunRepository.UpdateCellOutputAsync(cellOutput, ct);

                    if (result.Error is not null)
                    {
                        // Mark remaining code cells as skipped
                        await SkipRemainingCells(taskRun.Id, i + 1, cells, ct);
                        throw new InvalidOperationException(
                            $"Cell {i} failed: {result.Error.Ename}: {result.Error.Evalue}");
                    }
                }
                catch (InvalidOperationException)
                {
                    throw; // Re-throw cell failure
                }
                catch (Exception ex)
                {
                    cellOutput.Status = CellExecutionStatus.Failed;
                    cellOutput.CompletedAt = DateTime.UtcNow;
                    cellOutput.ErrorJson = JsonSerializer.Serialize(
                        new ErrorInfo("ExecutionError", ex.Message, []), JsonOptions);
                    await jobRunRepository.UpdateCellOutputAsync(cellOutput, ct);
                    await SkipRemainingCells(taskRun.Id, i + 1, cells, ct);
                    throw;
                }
            }
        }
        finally
        {
            if (nodeName is not null && kernelId is not null)
                await nodeManager.CleanupKernelAsync(nodeName, kernelId, ct);
        }
    }

    private async Task SkipRemainingCells(Guid taskRunId, int startIndex, List<CellInfo> cells, CancellationToken ct)
    {
        var cellOutputs = await jobRunRepository.GetCellOutputsAsync(taskRunId, ct);
        for (var j = startIndex; j < cells.Count; j++)
        {
            if (cells[j].Type != "Code") continue;
            var remaining = cellOutputs.FirstOrDefault(c => c.CellIndex == j);
            if (remaining is null) continue;
            remaining.Status = CellExecutionStatus.Skipped;
            remaining.CompletedAt = DateTime.UtcNow;
            await jobRunRepository.UpdateCellOutputAsync(remaining, ct);
        }
    }

    // ── SQL execution ───────────────────────────────────────────────

    private async Task ExecuteSqlQueryAsync(
        TaskRun taskRun, SqlQueryTask task, NodeManager nodeManager, CancellationToken ct)
    {
        var content = await workspaceReader.GetQueryContentAsync(task.QueryId, ct)
            ?? throw new InvalidOperationException(
                $"Query {task.QueryId} not found in workspace.");

        logger.LogInformation("Executing SQL query '{Title}'", content.Title);

        if (task.NodePoolRef is null)
            throw new InvalidOperationException(
                $"SQL query task '{task.Name}' requires a NodePoolRef to execute.");

        var (nodeName, kernelId) = await nodeManager.CreateKernelAsync(task.NodePoolRef, ct);
        taskRun.NodeName = nodeName;
        taskRun.KernelId = kernelId;
        await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);

        try
        {
            var code = $"import duckdb; duckdb.sql(\"\"\"{content.Content}\"\"\")";
            var result = await ExecuteCodeAsync(nodeName, kernelId, code, ct);

            taskRun.QueryResultJson = JsonSerializer.Serialize(result, JsonOptions);

            if (result.Error is not null)
                throw new InvalidOperationException(
                    $"SQL query failed: {result.Error.Ename}: {result.Error.Evalue}");
        }
        finally
        {
            await nodeManager.CleanupKernelAsync(nodeName, kernelId, ct);
        }
    }

    // ── SubJob execution ────────────────────────────────────────────

    private async Task ExecuteSubJobAsync(TaskRun taskRun, SubJobTask task, CancellationToken ct)
    {
        logger.LogInformation("Triggering sub-job {SubJobId} for task '{TaskName}'",
            task.SubJobId, task.Name);

        var subRun = await mediator.SendAsync(
            new TriggerJobRequest(task.SubJobId, task.Parameters, JobRunTrigger.SubJob), ct);

        // Set parent references
        subRun.ParentRunId = taskRun.JobRunId;
        subRun.ParentTaskRunId = taskRun.Id;
        // Note: these fields are set after create; the run dispatcher will pick up and execute the sub-run

        // Poll until sub-run completes
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(5), ct);

            var run = await jobRunRepository.GetJobRunAsync(subRun.Id, ct);
            if (run is null)
                throw new InvalidOperationException($"Sub-job run {subRun.Id} not found.");

            if (run.Status is JobRunStatus.Succeeded)
                return;

            if (run.Status is JobRunStatus.Failed)
                throw new InvalidOperationException($"Sub-job run {subRun.Id} failed.");

            if (run.Status is JobRunStatus.Cancelled)
                throw new OperationCanceledException($"Sub-job run {subRun.Id} was cancelled.");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private async Task<ExecutionResult> ExecuteCodeAsync(
        string nodeName, string kernelId, string code, CancellationToken ct)
    {
        var handle = await controlPlane.StartExecuteAsync(nodeName, kernelId, code, ct: ct);

        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            var poll = await controlPlane.PollExecutionAsync(nodeName, kernelId, handle.ExecutionId, ct);
            if (poll.IsComplete)
                return poll.Result!;
        }
    }

    // ── Notebook parsing ────────────────────────────────────────────

    internal record CellInfo(string Source, string Type, string Language);

    internal static List<CellInfo> ParseNotebookCells(string notebookJson)
    {
        var cells = new List<CellInfo>();
        using var doc = JsonDocument.Parse(notebookJson);

        if (!doc.RootElement.TryGetProperty("cells", out var cellsEl))
            return cells;

        foreach (var cellEl in cellsEl.EnumerateArray())
        {
            if (!cellEl.TryGetProperty("cell_type", out var typeEl))
                continue;

            var cellType = typeEl.GetString() == "markdown" ? "Markdown" : "Code";

            var source = ReadMultilineString(cellEl, "source");

            var language = "Python";
            if (cellEl.TryGetProperty("metadata", out var meta)
                && meta.TryGetProperty("language", out var langEl)
                && langEl.GetString() == "sql")
            {
                language = "Sql";
            }

            cells.Add(new CellInfo(source, cellType, language));
        }

        return cells;
    }

    private static string ReadMultilineString(JsonElement el, string propertyName)
    {
        if (!el.TryGetProperty(propertyName, out var prop)) return "";
        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? "",
            JsonValueKind.Array => string.Join("", prop.EnumerateArray().Select(l => l.GetString() ?? "")),
            _ => "",
        };
    }
}
