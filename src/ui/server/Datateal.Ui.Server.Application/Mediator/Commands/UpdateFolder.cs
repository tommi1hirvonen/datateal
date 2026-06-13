using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Server.Core.Workspace;
using Datateal.Ui.Shared.Workspace;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record UpdateFolderRequest(Guid WorkspaceId, Guid Id, string Name, Guid? ParentId) : IRequest<FolderSummary?>;

internal class UpdateFolderHandler(IWorkspaceRepository repository) : IRequestHandler<UpdateFolderRequest, FolderSummary?>
{
    public async Task<FolderSummary?> Handle(UpdateFolderRequest request, CancellationToken cancellationToken)
    {
        WorkspaceNameValidationException.ValidateNoSlash(request.Name);

        // Prevent cycles: walk up the proposed parent chain and reject if we encounter the folder itself.
        if (request.ParentId == request.Id)
            throw new InvalidOperationException("A folder cannot be its own parent.");

        if (request.ParentId.HasValue)
        {
            var cursor = request.ParentId;
            while (cursor.HasValue)
            {
                var ancestor = await repository.GetFolderAsync(request.WorkspaceId, cursor.Value, cancellationToken);
                if (ancestor is null) break;
                if (ancestor.ParentId == request.Id)
                    throw new InvalidOperationException("Cannot move a folder into one of its own descendants.");
                cursor = ancestor.ParentId;
            }
        }

        var folder = await repository.UpdateFolderAsync(request.WorkspaceId, request.Id, request.Name, request.ParentId, cancellationToken);
        return folder is null ? null : new FolderSummary(folder.Id, folder.Name, folder.ParentId, folder.CreatedAt);
    }
}
