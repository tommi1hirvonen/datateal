namespace DuckHouse.Ui.Shared.Workspace;

public record WorkspaceSearchResult(
    IReadOnlyList<NotebookSummary> Notebooks,
    IReadOnlyList<QuerySummary> Queries);
