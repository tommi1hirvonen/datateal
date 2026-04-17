namespace DuckHouse.Orchestrator.Core.Interfaces;

public record WorkspaceItemContent(Guid Id, string Title, string Content);

public interface IWorkspaceReader
{
    Task<WorkspaceItemContent?> GetNotebookContentAsync(Guid notebookId, CancellationToken ct = default);
    Task<WorkspaceItemContent?> GetQueryContentAsync(Guid queryId, CancellationToken ct = default);

    Task<Guid?> ResolveNotebookIdByPathAsync(string path, CancellationToken ct = default);
    Task<Guid?> ResolveQueryIdByPathAsync(string path, CancellationToken ct = default);
    Task<string?> ResolveNotebookPathByIdAsync(Guid id, CancellationToken ct = default);
    Task<string?> ResolveQueryPathByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetWorkspaceItemCatalogNamesAsync(Guid itemId, CancellationToken ct = default);
}
