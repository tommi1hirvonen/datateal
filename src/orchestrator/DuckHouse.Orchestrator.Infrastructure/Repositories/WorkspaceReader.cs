using DuckHouse.Core.Workspace;
using DuckHouse.Data;
using DuckHouse.Orchestrator.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DuckHouse.Orchestrator.Infrastructure.Repositories;

internal class WorkspaceReader(DuckHouseDbContext db) : IWorkspaceReader
{
    public async Task<WorkspaceItemContent?> GetNotebookContentAsync(Guid notebookId, CancellationToken ct)
    {
        return await db.WorkspaceItems
            .OfType<Notebook>()
            .Where(n => n.Id == notebookId)
            .Select(n => new WorkspaceItemContent(n.Id, n.Title, n.Content))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<WorkspaceItemContent?> GetQueryContentAsync(Guid queryId, CancellationToken ct)
    {
        return await db.WorkspaceItems
            .OfType<Query>()
            .Where(q => q.Id == queryId)
            .Select(q => new WorkspaceItemContent(q.Id, q.Title, q.Content))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Guid?> ResolveNotebookIdByPathAsync(string path, CancellationToken ct)
        => await ResolveItemIdByPathAsync<Notebook>(path, ct);

    public async Task<Guid?> ResolveQueryIdByPathAsync(string path, CancellationToken ct)
        => await ResolveItemIdByPathAsync<Query>(path, ct);

    public async Task<string?> ResolveNotebookPathByIdAsync(Guid id, CancellationToken ct)
        => await ResolveItemPathByIdAsync<Notebook>(id, ct);

    public async Task<string?> ResolveQueryPathByIdAsync(Guid id, CancellationToken ct)
        => await ResolveItemPathByIdAsync<Query>(id, ct);

    private async Task<Guid?> ResolveItemIdByPathAsync<T>(string path, CancellationToken ct)
        where T : WorkspaceItem
    {
        var normalized = path.Trim('/');
        var segments = normalized.Split('/');
        if (segments.Length == 0) return null;

        var itemTitle = segments[^1];
        var folderSegments = segments[..^1];

        Guid? folderId = null;
        foreach (var folderName in folderSegments)
        {
            folderId = await db.Folders
                .Where(f => f.Name == folderName && f.ParentId == folderId)
                .Select(f => (Guid?)f.Id)
                .FirstOrDefaultAsync(ct);

            if (folderId is null) return null;
        }

        return await db.WorkspaceItems
            .OfType<T>()
            .Where(i => i.Title == itemTitle && i.FolderId == folderId)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<string?> ResolveItemPathByIdAsync<T>(Guid id, CancellationToken ct)
        where T : WorkspaceItem
    {
        var item = await db.WorkspaceItems
            .OfType<T>()
            .Where(i => i.Id == id)
            .Select(i => new { i.Title, i.FolderId })
            .FirstOrDefaultAsync(ct);

        if (item is null) return null;

        var pathParts = new List<string> { item.Title };
        var folderId = item.FolderId;

        while (folderId is not null)
        {
            var folder = await db.Folders
                .Where(f => f.Id == folderId)
                .Select(f => new { f.Name, f.ParentId })
                .FirstOrDefaultAsync(ct);

            if (folder is null) break;

            pathParts.Add(folder.Name);
            folderId = folder.ParentId;
        }

        pathParts.Reverse();
        return "/" + string.Join("/", pathParts);
    }
}

