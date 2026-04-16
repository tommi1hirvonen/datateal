using System.Text.RegularExpressions;
using DuckHouse.Ui.Client.Services;

namespace DuckHouse.Ui.Client.Notebook;

/// <summary>
/// Handles parsing, detection, and expansion of <c>%run</c> magic lines in notebook cells.
/// </summary>
public static partial class RunMagicHelper
{
    private const int MaxDepth = 10;

    // Matches the path argument after %run, capturing everything to end-of-line
    // (including spaces). Quotes are stripped separately via StripQuotes().
    [GeneratedRegex(@"^\s*%run\s+(.+?)\s*$")]
    private static partial Regex RunLinePattern();

    /// <summary>
    /// Strips surrounding double or single quotes from a path string, if present.
    /// E.g. <c>"./My Folder/My Notebook"</c> → <c>./My Folder/My Notebook</c>.
    /// </summary>
    private static string StripQuotes(string path)
    {
        if (path.Length >= 2 &&
            ((path[0] == '"' && path[^1] == '"') ||
             (path[0] == '\'' && path[^1] == '\'')))
            return path[1..^1];
        return path;
    }

    /// <summary>Returns true if the source contains at least one <c>%run</c> line.</summary>
    public static bool HasRunLines(string source) =>
        source.AsSpan().IndexOf("%run") >= 0 && ParseRunLines(source).Count > 0;

    /// <summary>
    /// Detects <c>%run</c> lines in source code.
    /// Returns a list of (lineIndex, relativePath) pairs.
    /// </summary>
    public static IReadOnlyList<(int LineIndex, string Path)> ParseRunLines(string source)
    {
        var result = new List<(int, string)>();
        var lines = source.Split('\n');
        var pattern = RunLinePattern();
        for (int i = 0; i < lines.Length; i++)
        {
            var match = pattern.Match(lines[i]);
            if (match.Success)
                result.Add((i, StripQuotes(match.Groups[1].Value)));
        }
        return result;
    }

    /// <summary>
    /// Replaces all <c>%run</c> lines with <c>pass</c> statements.
    /// Used for diagnostics and prior-context of unexecuted cells.
    /// </summary>
    public static string ReplaceRunLinesWithPass(string source)
    {
        var pattern = RunLinePattern();
        var lines = source.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (pattern.IsMatch(lines[i]))
                lines[i] = "pass";
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Expands all <c>%run</c> lines in source code by resolving referenced workspace items.
    /// Supports recursive expansion with circular-reference detection.
    /// </summary>
    public static async Task<string> ExpandAsync(
        string source,
        Guid? baseFolderId,
        IWorkspaceService workspaceService,
        HashSet<Guid>? visited = null,
        int depth = 0)
    {
        if (depth > MaxDepth)
            throw new RunMagicException($"Maximum %run nesting depth ({MaxDepth}) exceeded.");

        visited ??= [];
        var runLines = ParseRunLines(source);
        if (runLines.Count == 0)
            return source;

        var lines = source.Split('\n');
        foreach (var (lineIndex, path) in runLines)
        {
            var resolved = await workspaceService.ResolvePathAsync(path, baseFolderId);
            if (resolved is null)
                throw new RunMagicException($"%run: item not found: {path}");

            if (resolved.Kind is not ("notebook" or "query"))
                throw new RunMagicException($"%run: '{path}' is a folder, not a notebook or query.");

            if (!visited.Add(resolved.Id))
                throw new RunMagicException($"%run: circular reference detected: {path}");

            string expandedCode = resolved.Kind == "notebook"
                ? ExpandNotebookContent(resolved.Content!)
                : WrapSqlContent(resolved.Content!);

            // Recursively expand any nested %run lines in the resolved code
            if (HasRunLines(expandedCode))
                expandedCode = await ExpandAsync(expandedCode, resolved.FolderId, workspaceService, visited, depth + 1);

            lines[lineIndex] = expandedCode;

            // Remove from visited after processing so the same item can be
            // referenced from independent branches (just not circularly)
            visited.Remove(resolved.Id);
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Extracts all code cells from a serialized notebook and concatenates their source.
    /// SQL cells are wrapped with <c>duckdb.sql()</c>.
    /// </summary>
    private static string ExpandNotebookContent(string notebookJson)
    {
        var doc = NotebookSerializer.Deserialize(notebookJson);
        var codeParts = new List<string>();
        foreach (var cell in doc.Cells)
        {
            if (cell.CellType != NotebookCellType.Code) continue;

            string cellCode = cell.Language == NotebookCellLanguage.Sql
                ? WrapSqlContent(cell.Source)
                : cell.Source;

            if (!string.IsNullOrWhiteSpace(cellCode))
                codeParts.Add(cellCode);
        }
        return string.Join("\n", codeParts);
    }

    private static string WrapSqlContent(string sql) =>
        $"import duckdb; duckdb.sql(\"\"\"{sql}\"\"\")";
}
