using DuckHouse.Ui.Shared.Workspace;

namespace DuckHouse.Ui.Client.Services;

public interface IWorkspaceService
{
    Task<WorkspaceSearchResult> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<WorkspaceListing> GetRootAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceListing> GetFolderAsync(Guid folderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FolderSummary>> GetFolderAncestorsAsync(Guid folderId, CancellationToken cancellationToken = default);
    Task<NotebookDetail?> GetNotebookAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ResolvedWorkspaceItem?> ResolvePathAsync(string relativePath, Guid? baseFolderId, CancellationToken cancellationToken = default);

    Task<FolderSummary> CreateFolderAsync(CreateFolderRequest request, CancellationToken cancellationToken = default);
    Task<FolderSummary?> UpdateFolderAsync(Guid id, UpdateFolderRequest request, CancellationToken cancellationToken = default);
    Task DeleteFolderAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WorkspaceItemSummary> CreateNotebookAsync(CreateNotebookRequest request, CancellationToken cancellationToken = default);
    Task<WorkspaceItemSummary?> UpdateNotebookAsync(Guid id, UpdateNotebookRequest request, CancellationToken cancellationToken = default);
    Task DeleteNotebookAsync(Guid id, CancellationToken cancellationToken = default);

    Task<QueryDetail?> GetQueryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkspaceItemSummary> CreateQueryAsync(CreateQueryRequest request, CancellationToken cancellationToken = default);
    Task<WorkspaceItemSummary?> UpdateQueryAsync(Guid id, UpdateQueryRequest request, CancellationToken cancellationToken = default);
    Task DeleteQueryAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveQueryResultAsync(Guid id, SaveQueryResultRequest request, CancellationToken cancellationToken = default);
}
