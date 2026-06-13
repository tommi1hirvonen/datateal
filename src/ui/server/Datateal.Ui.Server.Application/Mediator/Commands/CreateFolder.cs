using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Server.Core.Workspace;
using Datateal.Ui.Shared.Workspace;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record CreateFolderRequest(Guid WorkspaceId, string Name, Guid? ParentId) : IRequest<FolderSummary>;

internal class CreateFolderHandler(IWorkspaceRepository repository) : IRequestHandler<CreateFolderRequest, FolderSummary>
{
    public async Task<FolderSummary> Handle(CreateFolderRequest request, CancellationToken cancellationToken)
    {
        WorkspaceNameValidationException.ValidateNoSlash(request.Name);
        var folder = await repository.CreateFolderAsync(request.WorkspaceId, request.Name, request.ParentId, cancellationToken);
        return new FolderSummary(folder.Id, folder.Name, folder.ParentId, folder.CreatedAt);
    }
}
