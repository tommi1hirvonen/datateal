using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Workspace;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record UpdateFolderRequest(Guid Id, string Name, Guid? ParentId) : IRequest<FolderSummary?>;

internal class UpdateFolderHandler(IWorkspaceRepository repository) : IRequestHandler<UpdateFolderRequest, FolderSummary?>
{
    public async Task<FolderSummary?> Handle(UpdateFolderRequest request, CancellationToken cancellationToken)
    {
        // Prevent cycles: walk up the proposed parent chain and reject if we encounter the folder itself.
        if (request.ParentId == request.Id)
            throw new InvalidOperationException("A folder cannot be its own parent.");

        if (request.ParentId.HasValue)
        {
            var cursor = request.ParentId;
            while (cursor.HasValue)
            {
                var ancestor = await repository.GetFolderAsync(cursor.Value, cancellationToken);
                if (ancestor is null) break;
                if (ancestor.ParentId == request.Id)
                    throw new InvalidOperationException("Cannot move a folder into one of its own descendants.");
                cursor = ancestor.ParentId;
            }
        }

        var folder = await repository.UpdateFolderAsync(request.Id, request.Name, request.ParentId, cancellationToken);
        return folder is null ? null : new FolderSummary(folder.Id, folder.Name, folder.ParentId, folder.CreatedAt);
    }
}
