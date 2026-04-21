using DuckHouse.Core.Workspace;
using DuckHouse.Data;
using DuckHouse.Ui.Server.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DuckHouse.Ui.Server.Infrastructure.Data;

internal class WorkspaceRepository(DuckHouseDbContext db) : IWorkspaceRepository
{
    // ── Folders ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Folder>> GetFoldersInAsync(Guid? parentId, CancellationToken cancellationToken = default)
    {
        var query = parentId.HasValue
            ? db.Folders.Where(f => f.ParentId == parentId)
            : db.Folders.Where(f => f.ParentId == null);

        return await query.OrderBy(f => f.Name).ToListAsync(cancellationToken);
    }

    public Task<Folder?> GetFolderAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Folders.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Folder>> GetFolderAncestorsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var chain = new List<Folder>();
        var currentId = (Guid?)id;
        while (currentId.HasValue)
        {
            var folder = await db.Folders.FirstOrDefaultAsync(f => f.Id == currentId, cancellationToken);
            if (folder is null) break;
            chain.Insert(0, folder);
            currentId = folder.ParentId;
        }
        return chain;
    }

    public async Task<Folder> CreateFolderAsync(string name, Guid? parentId, CancellationToken cancellationToken = default)
    {
        var folder = new Folder
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            ParentId = parentId,
            CreatedAt = DateTime.UtcNow,
        };
        db.Folders.Add(folder);
        await db.SaveChangesAsync(cancellationToken);
        return folder;
    }

    public async Task<Folder?> UpdateFolderAsync(Guid id, string name, Guid? parentId, CancellationToken cancellationToken = default)
    {
        var folder = await db.Folders.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (folder is null) return null;

        folder.Name = name;
        folder.ParentId = parentId;
        await db.SaveChangesAsync(cancellationToken);
        return folder;
    }

    public async Task DeleteFolderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var folder = await db.Folders.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (folder is not null)
        {
            db.Folders.Remove(folder);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<Folder?> GetFolderByNameAsync(string name, Guid? parentId, CancellationToken cancellationToken = default) =>
        db.Folders.FirstOrDefaultAsync(f => f.Name == name && f.ParentId == parentId, cancellationToken);

    // ── Workspace listing ─────────────────────────────────────────────────

    public Task<WorkspaceItem?> GetItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.WorkspaceItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<WorkspaceItem?> GetItemByTitleAsync(string title, Guid? folderId, CancellationToken cancellationToken = default) =>
        db.WorkspaceItems.FirstOrDefaultAsync(i => i.Title == title && i.FolderId == folderId, cancellationToken);

    public Task<bool> WorkspaceItemTitleExistsAsync(string title, Guid? folderId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = db.WorkspaceItems.Where(i =>
            i.Title == title &&
            i.FolderId == folderId);

        if (excludeId.HasValue)
            query = query.Where(i => i.Id != excludeId.Value);

        return query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceItem>> GetItemsInAsync(Guid? folderId, CancellationToken cancellationToken = default)
    {
        var query = folderId.HasValue
            ? db.WorkspaceItems.Where(i => i.FolderId == folderId)
            : db.WorkspaceItems.Where(i => i.FolderId == null);

        return await query.OrderBy(i => i.Title).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceItem>> SearchItemsAsync(string query, CancellationToken cancellationToken = default)
    {
        IQueryable<WorkspaceItem> q = db.WorkspaceItems;
        if (!string.IsNullOrWhiteSpace(query))
        {
            var lower = query.ToLowerInvariant();
            q = q.Where(i => i.Title.ToLower().Contains(lower));
        }
        return await q.OrderBy(i => i.Title).ToListAsync(cancellationToken);
    }

    // ── Notebooks ─────────────────────────────────────────────────────────

    public Task<Notebook?> GetNotebookAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.WorkspaceItems.OfType<Notebook>().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<Notebook> CreateNotebookAsync(string title, string content, Guid? folderId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var notebook = new Notebook
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            Content = content,
            FolderId = folderId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.WorkspaceItems.Add(notebook);
        await db.SaveChangesAsync(cancellationToken);
        return notebook;
    }

    public async Task<Notebook?> UpdateNotebookAsync(Guid id, string title, string content, Guid? folderId, CancellationToken cancellationToken = default)
    {
        var notebook = await db.WorkspaceItems.OfType<Notebook>().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (notebook is null) return null;

        notebook.Title = title;
        notebook.Content = content;
        notebook.FolderId = folderId;
        notebook.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return notebook;
    }

    public async Task<bool> DeleteNotebookAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notebook = await db.WorkspaceItems.OfType<Notebook>().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (notebook is null) return false;

        db.WorkspaceItems.Remove(notebook);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Queries ───────────────────────────────────────────────────────────

    public Task<Query?> GetQueryAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.WorkspaceItems.OfType<Query>().FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public async Task<Query> CreateQueryAsync(string title, string content, Guid? folderId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = new Query
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            Content = content,
            FolderId = folderId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.WorkspaceItems.Add(query);
        await db.SaveChangesAsync(cancellationToken);
        return query;
    }

    public async Task<Query?> UpdateQueryAsync(Guid id, string title, string content, Guid? folderId, CancellationToken cancellationToken = default)
    {
        var query = await db.WorkspaceItems.OfType<Query>().FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (query is null) return null;

        query.Title = title;
        query.Content = content;
        query.FolderId = folderId;
        query.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return query;
    }

    public async Task<bool> DeleteQueryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await db.WorkspaceItems.OfType<Query>().FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (query is null) return false;

        db.WorkspaceItems.Remove(query);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SaveQueryResultAsync(Guid id, string status, double durationMs, DateTime executedAt, string? resultJson, CancellationToken cancellationToken = default)
    {
        var query = await db.WorkspaceItems.OfType<Query>().FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (query is null) return false;

        query.LastResultStatus = status;
        query.LastDurationMs = durationMs;
        query.LastExecutedAt = executedAt;
        query.LastResultJson = resultJson;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Catalog associations ─────────────────────────────────────────────

    public async Task<bool> UpdateItemCatalogNamesAsync(Guid itemId, List<string>? catalogNames, CancellationToken cancellationToken = default)
    {
        var item = await db.WorkspaceItems.FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item is null) return false;

        item.CatalogNames = catalogNames;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
