using System.Text.Json;
using System.Text.RegularExpressions;
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

    public async Task ExecuteAsync(TaskRun taskRun, JobTask task, NodeManager nodeManager,
        Dictionary<string, string>? jobRunParameters, CancellationToken ct)
    {
        switch (task)
        {
            case NotebookTask notebook:
                await ExecuteNotebookAsync(taskRun, notebook, nodeManager, jobRunParameters, ct);
                break;
            case SqlQueryTask sqlQuery:
                await ExecuteSqlQueryAsync(taskRun, sqlQuery, nodeManager, jobRunParameters, ct);
                break;
            case SubJobTask subJob:
                await ExecuteSubJobAsync(taskRun, subJob, jobRunParameters, ct);
                break;
            default:
                throw new InvalidOperationException($"Unknown task type: {task.GetType().Name}");
        }
    }

    // ── Notebook execution ──────────────────────────────────────────

    private async Task ExecuteNotebookAsync(
        TaskRun taskRun, NotebookTask task, NodeManager nodeManager,
        Dictionary<string, string>? jobRunParameters, CancellationToken ct)
    {
        var content = await workspaceReader.GetNotebookContentAsync(task.NotebookId, ct)
            ?? throw new InvalidOperationException(
                $"Notebook {task.NotebookId} not found in workspace.");

        var cells = ParseNotebookCells(content.Content);

        // Determine this notebook's folder path for %run relative path resolution
        var notebookAbsPath = await workspaceReader.ResolveNotebookPathByIdAsync(task.NotebookId, ct);
        var notebookFolderPath = notebookAbsPath is not null ? GetFolderPath(notebookAbsPath) : "";

        var resolvedParameters = ResolveParameters(task.Parameters, jobRunParameters);

        // Inject parameters cell if task has parameters and notebook has a parameter cell
        int paramCellIndex = cells.FindIndex(c => c.Tags.Contains("parameters"));
        if (paramCellIndex >= 0 && resolvedParameters is { Count: > 0 })
        {
            var injectedSource = BuildInjectedParametersSource(resolvedParameters);
            var injected = new CellInfo(injectedSource, "Code", "Python", ["injected-parameters"]);
            cells = [.. cells[..(paramCellIndex + 1)], injected, .. cells[(paramCellIndex + 1)..]];
        }

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
                CellRole = cell.Tags.Contains("parameters") ? "parameters"
                         : cell.Tags.Contains("injected-parameters") ? "injected-parameters"
                         : null,
                Status = CellExecutionStatus.Pending,
            }, ct);
        }

        // Provision node and kernel
        if (task.NodePoolRef is null)
            throw new InvalidOperationException(
                $"Notebook task '{task.Name}' requires a NodePoolRef to execute.");

        var (nodeName, kernelId) = await nodeManager.CreateKernelAsync(task.NodePoolRef, ct);
        taskRun.NodeName = nodeName;
        taskRun.KernelId = kernelId;
        await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);

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

                // Wrap SQL cells; expand %run magic in Python cells
                var code = cell.Language == "Sql"
                    ? $"import duckdb; duckdb.sql(\"\"\"{cell.Source}\"\"\")"
                    : cell.Source;

                if (cell.Language != "Sql" && HasRunLines(code))
                    code = await ExpandRunMagicAsync(code, notebookFolderPath, null, 0, ct);

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
        TaskRun taskRun, SqlQueryTask task, NodeManager nodeManager,
        Dictionary<string, string>? jobRunParameters, CancellationToken ct)
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

    private async Task ExecuteSubJobAsync(TaskRun taskRun, SubJobTask task,
        Dictionary<string, string>? jobRunParameters, CancellationToken ct)
    {
        logger.LogInformation("Triggering sub-job {SubJobId} for task '{TaskName}'",
            task.SubJobId, task.Name);

        var resolvedParameters = ResolveParameters(task.Parameters, jobRunParameters);

        var subRun = await mediator.SendAsync(
            new TriggerJobRequest(task.SubJobId, resolvedParameters, JobRunTrigger.SubJob), ct);

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

    internal record CellInfo(string Source, string Type, string Language, IReadOnlyList<string> Tags);

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
            List<string> tags = [];
            if (cellEl.TryGetProperty("metadata", out var meta))
            {
                if (meta.TryGetProperty("language", out var langEl) && langEl.GetString() == "sql")
                    language = "Sql";

                if (meta.TryGetProperty("tags", out var tagsEl))
                    tags = tagsEl.EnumerateArray()
                        .Select(t => t.GetString() ?? "")
                        .Where(t => t.Length > 0)
                        .ToList();
            }

            // Skip injected-parameters cells from prior runs stored in the notebook
            if (tags.Contains("injected-parameters")) continue;

            cells.Add(new CellInfo(source, cellType, language, tags));
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

    /// <summary>
    /// Resolves ${{ param_name }} tokens in task parameter values using job run parameter values.
    /// Unknown placeholders are left unchanged.
    /// </summary>
    internal static Dictionary<string, string>? ResolveParameters(
        Dictionary<string, string>? taskParameters,
        Dictionary<string, string>? jobRunParameters)
    {
        if (taskParameters is null or { Count: 0 }) return taskParameters;
        if (jobRunParameters is null or { Count: 0 }) return taskParameters;

        return taskParameters.ToDictionary(
            kv => kv.Key,
            kv => ResolveValue(kv.Value, jobRunParameters));
    }

    private static readonly Regex PlaceholderRegex =
        new(@"\$\{\{\s*(\w+)\s*\}\}", RegexOptions.Compiled);

    private static string ResolveValue(string value, Dictionary<string, string> jobRunParameters) =>
        PlaceholderRegex.Replace(value, m =>
        {
            var name = m.Groups[1].Value;
            return jobRunParameters.TryGetValue(name, out var resolved) ? resolved : m.Value;
        });

    // ── %run magic expansion ─────────────────────────────────────────

    private static readonly Regex RunMagicPattern =
        new(@"^\s*%run\s+(\S+)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

    private static bool HasRunLines(string source) => RunMagicPattern.IsMatch(source);

    /// <summary>
    /// Expands %run lines in <paramref name="source"/> by resolving referenced notebook/query
    /// content relative to <paramref name="baseFolderPath"/>. Supports recursive expansion with
    /// circular-reference detection and a depth limit.
    /// </summary>
    private async Task<string> ExpandRunMagicAsync(
        string source,
        string baseFolderPath,
        HashSet<Guid>? visited,
        int depth,
        CancellationToken ct)
    {
        if (depth > 10)
            throw new InvalidOperationException("Maximum %run nesting depth (10) exceeded.");

        visited ??= [];

        var lines = source.Split('\n');
        bool modified = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var match = RunMagicPattern.Match(lines[i]);
            if (!match.Success) continue;

            var relativePath = match.Groups[1].Value;
            var absolutePath = ResolvePath(baseFolderPath, relativePath);

            // Try notebook first
            var notebookId = await workspaceReader.ResolveNotebookIdByPathAsync(absolutePath, ct);
            if (notebookId is not null)
            {
                if (!visited.Add(notebookId.Value))
                    throw new InvalidOperationException(
                        $"%run: circular reference detected: {relativePath}");

                var refContent = await workspaceReader.GetNotebookContentAsync(notebookId.Value, ct)
                    ?? throw new InvalidOperationException(
                        $"%run: notebook not found: {relativePath}");

                var refCells = ParseNotebookCells(refContent.Content);
                var cellCode = string.Join("\n", refCells
                    .Where(c => c.Type == "Code")
                    .Select(c => c.Language == "Sql"
                        ? $"import duckdb; duckdb.sql(\"\"\"{c.Source}\"\"\")"
                        : c.Source)
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

                // Recurse using the referenced notebook's folder as the new base
                var refAbsPath = await workspaceReader.ResolveNotebookPathByIdAsync(notebookId.Value, ct);
                var refFolderPath = refAbsPath is not null ? GetFolderPath(refAbsPath) : baseFolderPath;
                cellCode = await ExpandRunMagicAsync(cellCode, refFolderPath, visited, depth + 1, ct);

                visited.Remove(notebookId.Value);
                lines[i] = cellCode;
                modified = true;
                continue;
            }

            // Try query
            var queryId = await workspaceReader.ResolveQueryIdByPathAsync(absolutePath, ct);
            if (queryId is not null)
            {
                if (!visited.Add(queryId.Value))
                    throw new InvalidOperationException(
                        $"%run: circular reference detected: {relativePath}");

                var refContent = await workspaceReader.GetQueryContentAsync(queryId.Value, ct)
                    ?? throw new InvalidOperationException(
                        $"%run: query not found: {relativePath}");

                lines[i] = $"import duckdb; duckdb.sql(\"\"\"{refContent.Content}\"\"\")";
                visited.Remove(queryId.Value);
                modified = true;
                continue;
            }

            throw new InvalidOperationException($"%run: item not found: {relativePath}");
        }

        return modified ? string.Join("\n", lines) : source;
    }

    /// <summary>
    /// Resolves a relative workspace path against a base folder path.
    /// E.g. base="/folder1" + relative="./env_var_test" → "folder1/env_var_test"
    /// </summary>
    private static string ResolvePath(string baseFolderPath, string relativePath)
    {
        var segments = baseFolderPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        foreach (var part in relativePath.Replace('\\', '/').Split('/'))
        {
            if (part is "." or "") continue;
            if (part == "..")
            {
                if (segments.Count > 0)
                    segments.RemoveAt(segments.Count - 1);
            }
            else
            {
                segments.Add(part);
            }
        }

        return string.Join("/", segments);
    }

    private static string GetFolderPath(string absoluteItemPath)
    {
        var idx = absoluteItemPath.LastIndexOf('/');
        return idx <= 0 ? "" : absoluteItemPath[..idx];
    }

    /// <summary>
    /// Builds a Python source string that assigns the given parameters as variables,
    /// using simple type inference: int, float, bool, or quoted string.
    /// </summary>
    internal static string BuildInjectedParametersSource(Dictionary<string, string> parameters)
    {
        var lines = parameters.Select(kvp =>
        {
            var value = kvp.Value;
            if (long.TryParse(value, out _))
                return $"{kvp.Key} = {value}";
            if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                return $"{kvp.Key} = {value}";
            if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
                return $"{kvp.Key} = True";
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
                return $"{kvp.Key} = False";
            var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"{kvp.Key} = \"{escaped}\"";
        });
        return string.Join("\n", lines);
    }
}
