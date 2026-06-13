using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record DeleteFolderRequest(Guid WorkspaceId, Guid Id) : IRequest<bool>;

internal class DeleteFolderHandler(IWorkspaceRepository repository) : IRequestHandler<DeleteFolderRequest, bool>
{
    public async Task<bool> Handle(DeleteFolderRequest request, CancellationToken cancellationToken)
    {
        var folder = await repository.GetFolderAsync(request.WorkspaceId, request.Id, cancellationToken);
        if (folder is null) return false;
        await repository.DeleteFolderAsync(request.WorkspaceId, request.Id, cancellationToken);
        return true;
    }
}
