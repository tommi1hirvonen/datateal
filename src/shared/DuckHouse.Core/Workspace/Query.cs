namespace DuckHouse.Core.Workspace;

public class Query : WorkspaceItem
{
    public DateTime? LastExecutedAt { get; set; }
    public double? LastDurationMs { get; set; }
    public string? LastResultStatus { get; set; }
    public string? LastResultJson { get; set; }
}
