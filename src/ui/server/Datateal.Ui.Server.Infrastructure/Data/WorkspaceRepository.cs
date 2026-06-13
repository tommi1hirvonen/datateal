using Datateal.Core.Workspace;
using Datateal.Data;
using Datateal.Ui.Server.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Ui.Server.Infrastructure.Data;

internal class WorkspaceRepository(DatatealDbContext db) : IWorkspaceRepository
{
    // ── Folders ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Folder>> GetFoldersInAsync(Guid workspaceId, Guid? parentId, CancellationToken cancellationToken = default)
    {
        var folders = db.Folders.Where(f => f.WorkspaceId == workspaceId);
        var query = parentId.HasValue
            ? folders.Where(f => f.ParentId == parentId)
            : folders.Where(f => f.ParentId == null);

        return await query.OrderBy(f => f.Name).ToListAsync(cancellationToken);
    }

    public Task<Folder?> GetFolderAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default) =>
        db.Folders
            .Where(f => f.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Folder>> GetFolderAncestorsAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default)
    {
        var chain = new List<Folder>();
        var currentId = (Guid?)id;
        while (currentId.HasValue)
        {
            var folder = await db.Folders
                .Where(f => f.WorkspaceId == workspaceId)
                .FirstOrDefaultAsync(f => f.Id == currentId, cancellationToken);
            if (folder is null) break;
            chain.Insert(0, folder);
            currentId = folder.ParentId;
        }
        return chain;
    }

    public async Task<Folder> CreateFolderAsync(Guid workspaceId, string name, Guid? parentId, CancellationToken cancellationToken = default)
    {
        var folder = new Folder
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            ParentId = parentId,
            WorkspaceId = workspaceId,
            CreatedAt = DateTime.UtcNow,
        };
        db.Folders.Add(folder);
        await db.SaveChangesAsync(cancellationToken);
        return folder;
    }

    public async Task<Folder?> UpdateFolderAsync(Guid workspaceId, Guid id, string name, Guid? parentId, CancellationToken cancellationToken = default)
    {
        var folder = await db.Folders
            .Where(f => f.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (folder is null) return null;

        folder.Name = name;
        folder.ParentId = parentId;
        await db.SaveChangesAsync(cancellationToken);
        return folder;
    }

    public async Task DeleteFolderAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default)
    {
        var folder = await db.Folders
            .Where(f => f.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (folder is not null)
        {
            db.Folders.Remove(folder);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<Folder?> GetFolderByNameAsync(Guid workspaceId, string name, Guid? parentId, CancellationToken cancellationToken = default) =>
        db.Folders
            .Where(f => f.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(f => f.Name == name && f.ParentId == parentId, cancellationToken);

    // ── Workspace listing ─────────────────────────────────────────────────

    public Task<WorkspaceItem?> GetItemAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default) =>
        db.WorkspaceItems
            .Where(i => i.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<WorkspaceItem?> GetItemByTitleAsync(Guid workspaceId, string title, Guid? folderId, CancellationToken cancellationToken = default) =>
        db.WorkspaceItems
            .Where(i => i.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(i => i.Title == title && i.FolderId == folderId, cancellationToken);

    public Task<bool> WorkspaceItemTitleExistsAsync(Guid workspaceId, string title, Guid? folderId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = db.WorkspaceItems
            .Where(i => i.WorkspaceId == workspaceId)
            .Where(i =>
            i.Title == title &&
            i.FolderId == folderId);

        if (excludeId.HasValue)
            query = query.Where(i => i.Id != excludeId.Value);

        return query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceItemHeader>> GetItemsInAsync(Guid workspaceId, Guid? folderId, CancellationToken cancellationToken = default)
    {
        var items = db.WorkspaceItems.Where(i => i.WorkspaceId == workspaceId);
        var query = folderId.HasValue
            ? items.Where(i => i.FolderId == folderId)
            : items.Where(i => i.FolderId == null);

        return await query
            .OrderBy(i => i.Title)
            .Select(i => new WorkspaceItemHeader(
                i.Id,
                i.Title,
                i.FolderId,
                i.ItemType,
                i.CreatedAt,
                i.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceItemHeader>> SearchItemsAsync(Guid workspaceId, string query, CancellationToken cancellationToken = default)
    {
        IQueryable<WorkspaceItem> q = db.WorkspaceItems.Where(i => i.WorkspaceId == workspaceId);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var lower = query.ToLowerInvariant();
            q = q.Where(i => i.Title.ToLower().Contains(lower));
        }
        return await q
            .OrderBy(i => i.Title)
            .Select(i => new WorkspaceItemHeader(
                i.Id,
                i.Title,
                i.FolderId,
                i.ItemType,
                i.CreatedAt,
                i.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    // ── Notebooks ─────────────────────────────────────────────────────────

    public Task<Notebook?> GetNotebookAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default) =>
        db.WorkspaceItems
            .Where(i => i.WorkspaceId == workspaceId)
            .OfType<Notebook>()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<Notebook> CreateNotebookAsync(Guid workspaceId, string title, string content, Guid? folderId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var notebook = new Notebook
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            Content = content,
            FolderId = folderId,
            WorkspaceId = workspaceId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.WorkspaceItems.Add(notebook);
        await db.SaveChangesAsync(cancellationToken);
        return notebook;
    }

    public async Task<Notebook?> UpdateNotebookAsync(Guid workspaceId, Guid id, string title, string content, Guid? folderId, CancellationToken cancellationToken = default)
    {
        var notebook = await db.WorkspaceItems
            .Where(i => i.WorkspaceId == workspaceId)
            .OfType<Notebook>()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (notebook is null) return null;

        notebook.Title = title;
        notebook.Content = content;
        notebook.FolderId = folderId;
        notebook.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return notebook;
    }

    public async Task<bool> DeleteNotebookAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default)
    {
        var notebook = await db.WorkspaceItems
            .Where(i => i.WorkspaceId == workspaceId)
            .OfType<Notebook>()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (notebook is null) return false;

        db.WorkspaceItems.Remove(notebook);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Queries ───────────────────────────────────────────────────────────

    public Task<Query?> GetQueryAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default) =>
        db.WorkspaceItems
            .Where(i => i.WorkspaceId == workspaceId)
            .OfType<Query>()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public async Task<Query> CreateQueryAsync(
        Guid workspaceId,
        string title,
        string content,
        Guid? folderId,
        string? lastResultStatus,
        double? lastDurationMs,
        DateTime? lastExecutedAt,
        string? lastResultJson,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = new Query
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            Content = content,
            FolderId = folderId,
            WorkspaceId = workspaceId,
            CreatedAt = now,
            UpdatedAt = now,
            LastResultStatus = lastResultStatus,
            LastDurationMs = lastDurationMs,
            LastExecutedAt = lastExecutedAt,
            LastResultJson = lastResultJson,
        };
        db.WorkspaceItems.Add(query);
        await db.SaveChangesAsync(cancellationToken);
        return query;
    }

    public async Task<Query?> UpdateQueryAsync(
        Guid workspaceId,
        Guid id,
        string title,
        string content,
        Guid? folderId,
        string? lastResultStatus,
        double? lastDurationMs,
        DateTime? lastExecutedAt,
        string? lastResultJson,
        CancellationToken cancellationToken = default)
    {
        var query = await db.WorkspaceItems
            .Where(i => i.WorkspaceId == workspaceId)
            .OfType<Query>()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (query is null) return null;

        query.Title = title;
        query.Content = content;
        query.FolderId = folderId;
        query.UpdatedAt = DateTime.UtcNow;
        if (lastExecutedAt.HasValue)
        {
            query.LastResultStatus = lastResultStatus;
            query.LastDurationMs = lastDurationMs;
            query.LastExecutedAt = lastExecutedAt;
            query.LastResultJson = lastResultJson;
        }
        await db.SaveChangesAsync(cancellationToken);
        return query;
    }

    public async Task<bool> DeleteQueryAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default)
    {
        var query = await db.WorkspaceItems
            .Where(i => i.WorkspaceId == workspaceId)
            .OfType<Query>()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (query is null) return false;

        db.WorkspaceItems.Remove(query);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Catalog associations ─────────────────────────────────────────────

    public async Task<bool> UpdateItemCatalogNamesAsync(Guid workspaceId, Guid itemId, List<string>? catalogNames, CancellationToken cancellationToken = default)
    {
        var item = await db.WorkspaceItems
            .Where(i => i.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item is null) return false;

        item.CatalogNames = catalogNames;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
