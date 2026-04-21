using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Workspace;

namespace DuckHouse.Ui.Server.Application.Mediator.Queries;

public record GetWorkspaceRequest(Guid? FolderId = null) : IRequest<WorkspaceListing>;

internal class GetWorkspaceHandler(IWorkspaceRepository repository) : IRequestHandler<GetWorkspaceRequest, WorkspaceListing>
{
    public async Task<WorkspaceListing> Handle(GetWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var folders = await repository.GetFoldersInAsync(request.FolderId, cancellationToken);
        var items = await repository.GetItemsInAsync(request.FolderId, cancellationToken);

        var folderSummaries = folders
            .Select(f => new FolderSummary(f.Id, f.Name, f.ParentId, f.CreatedAt))
            .ToList();

        var itemSummaries = items
            .Select(h => new WorkspaceItemSummary(h.Id, h.Title, h.FolderId, h.ItemType, h.CreatedAt, h.UpdatedAt))
            .ToList();

        return new WorkspaceListing(folderSummaries, itemSummaries);
    }
}
