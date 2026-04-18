namespace DuckHouse.Orchestrator.Core.Entities;

public class RunNotebook
{
    public string Title { get; set; } = "";
    public List<RunCell> Cells { get; set; } = [];
}

public class RunCell
{
    public int Index { get; set; }
    public string Source { get; set; } = "";
    public string CellType { get; set; } = "Code";   // "Code" or "Markdown"
    public string Language { get; set; } = "Python"; // "Python" or "Sql"
    public string? CellRole { get; set; }             // null, "parameters", "injected-parameters"
    public string Status { get; set; } = "Pending";  // Pending/Running/Succeeded/Failed/Skipped
    public string? OutputsJson { get; set; }
    public string? ErrorJson { get; set; }
    public int? ExecutionCount { get; set; }
    public double? DurationMs { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
