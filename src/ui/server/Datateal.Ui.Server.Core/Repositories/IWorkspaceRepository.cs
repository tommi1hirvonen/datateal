using Datateal.Core.Workspace;

namespace Datateal.Ui.Server.Core.Repositories;

public interface IWorkspaceRepository
{
    // Folders
    Task<IReadOnlyList<Folder>> GetFoldersInAsync(Guid workspaceId, Guid? parentId, CancellationToken cancellationToken = default);
    Task<Folder?> GetFolderAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default);
    /// <summary>Returns the ancestor chain from root down to the folder with <paramref name="id"/>, inclusive.</summary>
    Task<IReadOnlyList<Folder>> GetFolderAncestorsAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default);
    Task<Folder> CreateFolderAsync(Guid workspaceId, string name, Guid? parentId, CancellationToken cancellationToken = default);
    Task<Folder?> UpdateFolderAsync(Guid workspaceId, Guid id, string name, Guid? parentId, CancellationToken cancellationToken = default);
    Task DeleteFolderAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Finds a child folder by name within a parent folder.</summary>
    Task<Folder?> GetFolderByNameAsync(Guid workspaceId, string name, Guid? parentId, CancellationToken cancellationToken = default);

    // Workspace listing — return lean headers; Content is never loaded
    Task<WorkspaceItem?> GetItemAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkspaceItemHeader>> GetItemsInAsync(Guid workspaceId, Guid? folderId, CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive substring search across all folders. Does not load Content.</summary>
    Task<IReadOnlyList<WorkspaceItemHeader>> SearchItemsAsync(Guid workspaceId, string query, CancellationToken cancellationToken = default);

    /// <summary>Finds a workspace item (notebook or query) by title within a folder.</summary>
    Task<WorkspaceItem?> GetItemByTitleAsync(Guid workspaceId, string title, Guid? folderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if any WorkspaceItem (notebook or query) in <paramref name="folderId"/> has
    /// <paramref name="title"/> as its title, excluding the item with <paramref name="excludeId"/> (for updates).
    /// </summary>
    Task<bool> WorkspaceItemTitleExistsAsync(Guid workspaceId, string title, Guid? folderId, Guid? excludeId = null, CancellationToken cancellationToken = default);

    // Notebooks
    Task<Notebook?> GetNotebookAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default);
    Task<Notebook> CreateNotebookAsync(Guid workspaceId, string title, string content, Guid? folderId, CancellationToken cancellationToken = default);
    Task<Notebook?> UpdateNotebookAsync(Guid workspaceId, Guid id, string title, string content, Guid? folderId, CancellationToken cancellationToken = default);
    Task<bool> DeleteNotebookAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default);

    // Queries
    Task<Query?> GetQueryAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default);
    Task<Query> CreateQueryAsync(
        Guid workspaceId,
        string title,
        string content,
        Guid? folderId,
        string? lastResultStatus,
        double? lastDurationMs,
        DateTime? lastExecutedAt,
        string? lastResultJson,
        CancellationToken cancellationToken = default);
    Task<Query?> UpdateQueryAsync(
        Guid workspaceId,
        Guid id,
        string title,
        string content,
        Guid? folderId,
        string? lastResultStatus,
        double? lastDurationMs,
        DateTime? lastExecutedAt,
        string? lastResultJson,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteQueryAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default);

    // Catalog associations
    Task<bool> UpdateItemCatalogNamesAsync(Guid workspaceId, Guid itemId, List<string>? catalogNames, CancellationToken cancellationToken = default);
}
