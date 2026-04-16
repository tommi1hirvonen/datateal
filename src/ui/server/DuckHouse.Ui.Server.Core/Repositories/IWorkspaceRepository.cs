using DuckHouse.Core.Workspace;

namespace DuckHouse.Ui.Server.Core.Repositories;

public interface IWorkspaceRepository
{
    // Folders
    Task<IReadOnlyList<Folder>> GetFoldersInAsync(Guid? parentId, CancellationToken cancellationToken = default);
    Task<Folder?> GetFolderAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Returns the ancestor chain from root down to the folder with <paramref name="id"/>, inclusive.</summary>
    Task<IReadOnlyList<Folder>> GetFolderAncestorsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Folder> CreateFolderAsync(string name, Guid? parentId, CancellationToken cancellationToken = default);
    Task<Folder?> UpdateFolderAsync(Guid id, string name, Guid? parentId, CancellationToken cancellationToken = default);
    Task DeleteFolderAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Finds a child folder by name within a parent folder.</summary>
    Task<Folder?> GetFolderByNameAsync(string name, Guid? parentId, CancellationToken cancellationToken = default);

    // Workspace listing (returns polymorphic items — callers use pattern matching)
    Task<IReadOnlyList<WorkspaceItem>> GetItemsInAsync(Guid? folderId, CancellationToken cancellationToken = default);

    /// <summary>Finds a workspace item (notebook or query) by title within a folder.</summary>
    Task<WorkspaceItem?> GetItemByTitleAsync(string title, Guid? folderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if any WorkspaceItem (notebook or query) in <paramref name="folderId"/> has
    /// <paramref name="title"/> as its title, excluding the item with <paramref name="excludeId"/> (for updates).
    /// </summary>
    Task<bool> WorkspaceItemTitleExistsAsync(string title, Guid? folderId, Guid? excludeId = null, CancellationToken cancellationToken = default);

    // Notebooks
    Task<Notebook?> GetNotebookAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Notebook> CreateNotebookAsync(string title, string content, Guid? folderId, CancellationToken cancellationToken = default);
    Task<Notebook?> UpdateNotebookAsync(Guid id, string title, string content, Guid? folderId, CancellationToken cancellationToken = default);
    Task<bool> DeleteNotebookAsync(Guid id, CancellationToken cancellationToken = default);

    // Queries
    Task<Query?> GetQueryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Query> CreateQueryAsync(string title, string content, Guid? folderId, CancellationToken cancellationToken = default);
    Task<Query?> UpdateQueryAsync(Guid id, string title, string content, Guid? folderId, CancellationToken cancellationToken = default);
    Task<bool> DeleteQueryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SaveQueryResultAsync(Guid id, string status, double durationMs, DateTime executedAt, string? resultJson, CancellationToken cancellationToken = default);
}
