using System.Text.Json;
using System.Text.RegularExpressions;
using DuckHouse.Core.Kernels;
using DuckHouse.Core.Mediator;
using DuckHouse.Core.Catalogs;
using DuckHouse.Orchestrator.Application.Mediator.Commands;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Enums;
using DuckHouse.Orchestrator.Core.Interfaces;
using DuckHouse.Orchestrator.Core.Repositories;
using Microsoft.Extensions.Logging;
using DuckHouse.Core.Orchestration;

namespace DuckHouse.Orchestrator.Application.Engine;

/// <summary>
/// Executes individual tasks based on their type (Notebook, SQL, SubJob).
/// </summary>
public class TaskExecutor(
    IControlPlaneClient controlPlane,
    IWorkspaceReader workspaceReader,
    IJobRunRepository jobRunRepository,
    ICatalogResolver catalogResolver,
    IMediator mediator,
    ILogger<TaskExecutor> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ExecuteAsync(TaskRun taskRun, JobTask task, NodeManager nodeManager,
        Dictionary<string, string>? jobRunParameters, CancellationToken ct)
    {
        switch ((taskRun, task))
        {
            case (NotebookTaskRun nbRun, NotebookTask nbTask):
                await ExecuteNotebookAsync(nbRun, nbTask, nodeManager, jobRunParameters, ct);
                break;
            case (SqlQueryTaskRun sqlRun, SqlQueryTask sqlTask):
                await ExecuteSqlQueryAsync(sqlRun, sqlTask, nodeManager, jobRunParameters, ct);
                break;
            case (SubJobTaskRun subRun, SubJobTask subJobTask):
                await ExecuteSubJobAsync(subRun, subJobTask, jobRunParameters, ct);
                break;
            default:
                throw new InvalidOperationException(
                    $"Mismatched task run type {taskRun.GetType().Name} and task type {task.GetType().Name}.");
        }
    }

    // ── Notebook execution ──────────────────────────────────────────

    private async Task ExecuteNotebookAsync(
        NotebookTaskRun taskRun, NotebookTask task, NodeManager nodeManager,
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

        // Build in-memory run notebook with all cells initialised to Pending
        var runCells = cells.Select((c, i) => new RunCell
        {
            Index = i,
            Source = c.Source,
            CellType = c.Type,
            Language = c.Language,
            CellRole = c.Tags.Contains("parameters") ? "parameters"
                     : c.Tags.Contains("injected-parameters") ? "injected-parameters"
                     : null,
            Status = "Pending",
        }).ToList();

        var notebook = new RunNotebook { Title = content.Title, Cells = runCells };
        taskRun.OutputJson = JsonSerializer.Serialize(notebook, JsonOptions);
        await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);

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
            // Attach DuckLake catalogs if configured
            await SetupCatalogsForWorkspaceItemAsync(task.NotebookId, nodeName, kernelId, ct);

            for (var i = 0; i < cells.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var cell = cells[i];
                if (cell.Type != "Code")
                    continue;

                var runCell = runCells[i];
                runCell.Status = "Running";
                runCell.StartedAt = DateTime.UtcNow;
                taskRun.OutputJson = JsonSerializer.Serialize(notebook, JsonOptions);
                await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);

                // Wrap SQL cells; expand %run magic in Python cells
                var code = cell.Language == "Sql"
                    ? WrapSqlContent(cell.Source)
                    : cell.Source;

                if (cell.Language != "Sql" && HasRunLines(code))
                    code = await ExpandRunMagicAsync(code, notebookFolderPath, null, 0, ct);

                try
                {
                    var result = await ExecuteCodeAsync(nodeName, kernelId, code, ct);

                    runCell.Status = result.Error is not null ? "Failed" : "Succeeded";
                    runCell.CompletedAt = DateTime.UtcNow;
                    runCell.DurationMs = result.DurationMs;
                    runCell.ExecutionCount = result.ExecutionCount;
                    runCell.OutputsJson = JsonSerializer.Serialize(result.Outputs, JsonOptions);
                    if (result.Error is not null)
                        runCell.ErrorJson = JsonSerializer.Serialize(result.Error, JsonOptions);

                    taskRun.OutputJson = JsonSerializer.Serialize(notebook, JsonOptions);
                    await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);

                    if (result.Error is not null)
                    {
                        await SkipRemainingCells(taskRun, notebook, runCells, i + 1, cells, ct);
                        throw new InvalidOperationException(
                            $"Cell {i} failed: {result.Error.Ename}: {result.Error.Evalue}");
                    }
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    runCell.Status = "Failed";
                    runCell.CompletedAt = DateTime.UtcNow;
                    runCell.ErrorJson = JsonSerializer.Serialize(
                        new ErrorInfo("ExecutionError", ex.Message, []), JsonOptions);
                    taskRun.OutputJson = JsonSerializer.Serialize(notebook, JsonOptions);
                    await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);
                    await SkipRemainingCells(taskRun, notebook, runCells, i + 1, cells, ct);
                    throw;
                }
            }
        }
        finally
        {
            await nodeManager.CleanupKernelAsync(nodeName, kernelId, ct);
        }
    }

    private async Task SkipRemainingCells(ComputeTaskRun taskRun, RunNotebook notebook, List<RunCell> runCells,
        int startIndex, List<CellInfo> cells, CancellationToken ct)
    {
        for (var j = startIndex; j < cells.Count; j++)
        {
            if (cells[j].Type != "Code") continue;
            runCells[j].Status = "Skipped";
            runCells[j].CompletedAt = DateTime.UtcNow;
        }
        taskRun.OutputJson = JsonSerializer.Serialize(notebook, JsonOptions);
        await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);
    }

    // ── SQL execution ───────────────────────────────────────────────

    private async Task ExecuteSqlQueryAsync(
        SqlQueryTaskRun taskRun, SqlQueryTask task, NodeManager nodeManager,
        Dictionary<string, string>? jobRunParameters, CancellationToken ct)
    {
        var content = await workspaceReader.GetQueryContentAsync(task.QueryId, ct)
            ?? throw new InvalidOperationException(
                $"Query {task.QueryId} not found in workspace.");

        logger.LogInformation("Executing SQL query '{Title}'", content.Title);

        if (task.NodePoolRef is null)
            throw new InvalidOperationException(
                $"SQL query task '{task.Name}' requires a NodePoolRef to execute.");

        var runCell = new RunCell
        {
            Index = 0,
            Source = content.Content,
            CellType = "Code",
            Language = "Sql",
            Status = "Pending",
        };
        var notebook = new RunNotebook { Title = content.Title, Cells = [runCell] };
        taskRun.OutputJson = JsonSerializer.Serialize(notebook, JsonOptions);
        await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);

        var (nodeName, kernelId) = await nodeManager.CreateKernelAsync(task.NodePoolRef, ct);
        taskRun.NodeName = nodeName;
        taskRun.KernelId = kernelId;
        await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);

        try
        {
            // Attach DuckLake catalogs if configured
            await SetupCatalogsForWorkspaceItemAsync(task.QueryId, nodeName, kernelId, ct);

            runCell.Status = "Running";
            runCell.StartedAt = DateTime.UtcNow;
            taskRun.OutputJson = JsonSerializer.Serialize(notebook, JsonOptions);
            await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);

            var code = WrapSqlContent(content.Content);
            var result = await ExecuteCodeAsync(nodeName, kernelId, code, ct);

            runCell.CompletedAt = DateTime.UtcNow;
            runCell.DurationMs = result.DurationMs;
            runCell.ExecutionCount = result.ExecutionCount;
            runCell.OutputsJson = JsonSerializer.Serialize(result.Outputs, JsonOptions);

            if (result.Error is not null)
            {
                runCell.Status = "Failed";
                runCell.ErrorJson = JsonSerializer.Serialize(result.Error, JsonOptions);
                taskRun.OutputJson = JsonSerializer.Serialize(notebook, JsonOptions);
                await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);
                throw new InvalidOperationException(
                    $"SQL query failed: {result.Error.Ename}: {result.Error.Evalue}");
            }

            runCell.Status = "Succeeded";
            taskRun.OutputJson = JsonSerializer.Serialize(notebook, JsonOptions);
            await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            runCell.Status = "Failed";
            runCell.CompletedAt = DateTime.UtcNow;
            runCell.ErrorJson = JsonSerializer.Serialize(
                new ErrorInfo("ExecutionError", ex.Message, []), JsonOptions);
            taskRun.OutputJson = JsonSerializer.Serialize(notebook, JsonOptions);
            await jobRunRepository.UpdateTaskRunAsync(taskRun, ct);
            throw;
        }
        finally
        {
            await nodeManager.CleanupKernelAsync(nodeName, kernelId, ct);
        }
    }

    // ── SubJob execution ────────────────────────────────────────────

    private async Task ExecuteSubJobAsync(SubJobTaskRun taskRun, SubJobTask task,
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
        new(@"^\s*%run\s+(.+?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

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

            var relativePath = StripRunMagicQuotes(match.Groups[1].Value);
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
                        ? WrapSqlContent(c.Source)
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

                lines[i] = WrapSqlContent(refContent.Content);
                visited.Remove(queryId.Value);
                modified = true;
                continue;
            }

            throw new InvalidOperationException($"%run: item not found: {relativePath}");
        }

        return modified ? string.Join("\n", lines) : source;
    }

    /// <summary>
    /// Strips surrounding double or single quotes from a path extracted from a %run line.
    /// E.g. <c>"./My Folder/My Notebook"</c> → <c>./My Folder/My Notebook</c>.
    /// </summary>
    private static string StripRunMagicQuotes(string path) =>
        path.Length >= 2 &&
        ((path[0] == '"' && path[^1] == '"') || (path[0] == '\'' && path[^1] == '\''))
            ? path[1..^1]
            : path;

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

    private static string WrapSqlContent(string sql) =>
        // DuckDB's EXPLAIN output can be very wide and doesn't fit well in the DataFrame view,
        // so we print it as text instead if the expected columns are present.
        $""""
        import duckdb
        __df = duckdb.execute("""{sql}""").df()
        __result = None
        if all(value in __df.columns for value in ['explain_key', 'explain_value']):
            print('\\n'.join(str(v) for v in __df['explain_value']))
        else:
            __result = __df
        __result
        """";

    // ── Catalog setup ───────────────────────────────────────────────

    private async Task SetupCatalogsForWorkspaceItemAsync(
        Guid workspaceItemId, string nodeName, string kernelId, CancellationToken ct)
    {
        var catalogNames = await workspaceReader.GetWorkspaceItemCatalogNamesAsync(workspaceItemId, ct);
        if (catalogNames.Count == 0) return;

        var resolved = await catalogResolver.ResolveAsync(catalogNames, ct);
        if (resolved.Count == 0) return;

        var script = CatalogSetupGenerator.GenerateSetupScript(resolved);
        logger.LogInformation("Attaching {Count} catalog(s) to kernel {KernelId}", resolved.Count, kernelId);

        var result = await ExecuteCodeAsync(nodeName, kernelId, script, ct);
        if (result.Error is not null)
        {
            throw new InvalidOperationException(
                $"Catalog setup failed: {result.Error.Ename}: {result.Error.Evalue}");
        }
    }
}
