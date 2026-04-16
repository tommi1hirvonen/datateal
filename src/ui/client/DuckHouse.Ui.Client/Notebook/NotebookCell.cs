using DuckHouse.Core.Kernels;

namespace DuckHouse.Ui.Client.Notebook;

public enum NotebookCellType { Code, Markdown }

public enum NotebookCellLanguage { Python, Sql }

public class NotebookCell
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public NotebookCellType CellType { get; set; }
    public NotebookCellLanguage Language { get; set; } = NotebookCellLanguage.Python;
    public string Source { get; set; } = "";
    public bool IsParameterCell { get; set; }
    public List<Output> Outputs { get; set; } = [];
    public ErrorInfo? Error { get; set; }
    public int? ExecutionCount { get; set; }
    public double? DurationMs { get; set; }

    // ── UI state (not serialized) ────────────────────────────────────
    public bool IsExecuting { get; set; }
    public string? ExecutionId { get; set; }
    public bool IsEditingMarkdown { get; set; }
    /// <summary>
    /// Cached expanded source after <c>%run</c> resolution. Set after execution,
    /// used by PriorContextFor() to provide expanded context for completions/diagnostics.
    /// </summary>
    public string? CachedExpandedSource { get; set; }

    public void ClearOutput()
    {
        Outputs.Clear();
        Error = null;
        DurationMs = null;
        CachedExpandedSource = null;
    }
}
