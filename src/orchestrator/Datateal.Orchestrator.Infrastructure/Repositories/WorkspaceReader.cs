using Datateal.Core.Workspace;
using Datateal.Data;
using Datateal.Orchestrator.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Orchestrator.Infrastructure.Repositories;

internal class WorkspaceReader(DatatealDbContext db) : IWorkspaceReader
{
    public async Task<WorkspaceItemContent?> GetNotebookContentAsync(Guid notebookId, CancellationToken ct)
        => await db.WorkspaceItems
            .OfType<Notebook>()
            .Where(n => n.Id == notebookId)
            .Select(n => new WorkspaceItemContent(n.Id, n.Title, n.Content))
            .FirstOrDefaultAsync(ct);

    public async Task<WorkspaceItemContent?> GetQueryContentAsync(Guid queryId, CancellationToken ct)
        => await db.WorkspaceItems
            .OfType<Query>()
            .Where(q => q.Id == queryId)
            .Select(q => new WorkspaceItemContent(q.Id, q.Title, q.Content))
            .FirstOrDefaultAsync(ct);

    public async Task<Guid?> ResolveNotebookIdByPathAsync(Guid workspaceId, string path, CancellationToken ct)
        => await ResolveItemIdByPathAsync<Notebook>(db, workspaceId, path, ct);

    public async Task<Guid?> ResolveQueryIdByPathAsync(Guid workspaceId, string path, CancellationToken ct)
        => await ResolveItemIdByPathAsync<Query>(db, workspaceId, path, ct);

    public async Task<IReadOnlyList<string>> GetWorkspaceItemCatalogNamesAsync(Guid itemId, CancellationToken ct)
    {
        var catalogNames = await db.WorkspaceItems
            .Where(i => i.Id == itemId)
            .Select(i => i.CatalogNames)
            .FirstOrDefaultAsync(ct);

        return catalogNames ?? [];
    }

    private static async Task<Guid?> ResolveItemIdByPathAsync<T>(DatatealDbContext db, Guid workspaceId, string path, CancellationToken ct)
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
                .Where(f => f.WorkspaceId == workspaceId && f.Name == folderName && f.ParentId == folderId)
                .Select(f => (Guid?)f.Id)
                .FirstOrDefaultAsync(ct);

            if (folderId is null) return null;
        }

        return await db.WorkspaceItems
            .OfType<T>()
            .Where(i => i.WorkspaceId == workspaceId && i.Title == itemTitle && i.FolderId == folderId)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(ct);
    }

}
