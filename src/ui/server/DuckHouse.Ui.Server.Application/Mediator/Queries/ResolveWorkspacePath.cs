using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Server.Core.Workspace;
using DuckHouse.Ui.Shared.Workspace;

namespace DuckHouse.Ui.Server.Application.Mediator.Queries;

public record ResolveWorkspacePathRequest(string Path, Guid? BaseFolderId) : IRequest<ResolvedWorkspaceItem?>;

internal class ResolveWorkspacePathHandler(IWorkspaceRepository repository)
    : IRequestHandler<ResolveWorkspacePathRequest, ResolvedWorkspaceItem?>
{
    public async Task<ResolvedWorkspaceItem?> Handle(ResolveWorkspacePathRequest request, CancellationToken cancellationToken)
    {
        var segments = ParseSegments(request.Path);
        if (segments.Count == 0)
            return null;

        // Navigate to the target folder through all intermediate segments
        Guid? currentFolderId = request.BaseFolderId;

        for (int i = 0; i < segments.Count - 1; i++)
        {
            currentFolderId = await NavigateSegmentAsync(segments[i], currentFolderId, cancellationToken);
            if (currentFolderId == NavigationFailed)
                return null;
        }

        string lastSegment = segments[^1];

        // Empty last segment (path ended with /) → return folder listing
        if (string.IsNullOrEmpty(lastSegment))
            return await BuildFolderResultAsync(currentFolderId, cancellationToken);

        // ".." as last segment → navigate up and return folder listing
        if (lastSegment == "..")
        {
            currentFolderId = await NavigateUpAsync(currentFolderId, cancellationToken);
            if (currentFolderId == NavigationFailed)
                return null;
            return await BuildFolderResultAsync(currentFolderId, cancellationToken);
        }

        // Try to find a workspace item (notebook or query) by title
        var item = await repository.GetItemByTitleAsync(lastSegment, currentFolderId, cancellationToken);
        if (item is not null)
        {
            string kind = item is Notebook ? "notebook" : "query";
            return new ResolvedWorkspaceItem(kind, item.Id, item.Title, item.FolderId, item.Content, null);
        }

        // Try to find a folder by name and return its listing
        var folder = await repository.GetFolderByNameAsync(lastSegment, currentFolderId, cancellationToken);
        if (folder is not null)
            return await BuildFolderResultAsync(folder.Id, cancellationToken);

        return null;
    }

    /// <summary>Sentinel value indicating folder navigation failed.</summary>
    private static readonly Guid? NavigationFailed = Guid.Empty;

    private async Task<Guid?> NavigateSegmentAsync(string segment, Guid? currentFolderId, CancellationToken ct)
    {
        if (segment is "." or "")
            return currentFolderId;

        if (segment == "..")
            return await NavigateUpAsync(currentFolderId, ct);

        // Navigate into a child folder by name
        var child = await repository.GetFolderByNameAsync(segment, currentFolderId, ct);
        return child?.Id ?? NavigationFailed;
    }

    private async Task<Guid?> NavigateUpAsync(Guid? currentFolderId, CancellationToken ct)
    {
        if (currentFolderId is null)
            return null; // Already at root, stay at root

        var current = await repository.GetFolderAsync(currentFolderId.Value, ct);
        return current?.ParentId; // null ParentId means parent is root
    }

    private async Task<ResolvedWorkspaceItem?> BuildFolderResultAsync(Guid? folderId, CancellationToken ct)
    {
        var folders = await repository.GetFoldersInAsync(folderId, ct);
        var items = await repository.GetItemsInAsync(folderId, ct);

        var listing = new WorkspaceListing(
            folders.Select(f => new FolderSummary(f.Id, f.Name, f.ParentId, f.CreatedAt)).ToList(),
            items.OfType<Notebook>().Select(n => new NotebookSummary(n.Id, n.Title, n.FolderId, n.CreatedAt, n.UpdatedAt)).ToList(),
            items.OfType<Query>().Select(q => new QuerySummary(q.Id, q.Title, q.FolderId, q.CreatedAt, q.UpdatedAt)).ToList());

        if (folderId is null)
            return new ResolvedWorkspaceItem("folder", Guid.Empty, "", null, null, listing);

        var folder = await repository.GetFolderAsync(folderId.Value, ct);
        if (folder is null) return null;

        return new ResolvedWorkspaceItem("folder", folder.Id, folder.Name, folder.ParentId, null, listing);
    }

    private static List<string> ParseSegments(string path)
    {
        // Normalize separators and split
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.None);

        // Filter leading empty segment (from paths like "./foo")
        var segments = new List<string>();
        foreach (var part in parts)
        {
            // Skip standalone "." at the beginning
            if (segments.Count == 0 && part == ".")
                continue;
            segments.Add(part);
        }

        return segments;
    }
}
