using Datateal.Core.Mediator;
using Datateal.Core.Workspace;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Server.Core.Workspace;
using Datateal.Ui.Shared.Workspace;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record UpdateNotebookRequest(Guid WorkspaceId, Guid Id, string Title, string Content, Guid? FolderId) : IRequest<WorkspaceItemSummary?>;

internal class UpdateNotebookHandler(IWorkspaceRepository repository) : IRequestHandler<UpdateNotebookRequest, WorkspaceItemSummary?>
{
    public async Task<WorkspaceItemSummary?> Handle(UpdateNotebookRequest request, CancellationToken cancellationToken)
    {
        WorkspaceNameValidationException.ValidateNoSlash(request.Title);
        if (await repository.WorkspaceItemTitleExistsAsync(request.WorkspaceId, request.Title, request.FolderId, excludeId: request.Id, cancellationToken: cancellationToken))
            throw new WorkspaceTitleConflictException(request.Title, request.FolderId is null ? "the root folder" : "this folder");

        var notebook = await repository.UpdateNotebookAsync(request.WorkspaceId, request.Id, request.Title, request.Content, request.FolderId, cancellationToken);
        return notebook is null ? null : new WorkspaceItemSummary(notebook.Id, notebook.Title, notebook.FolderId, WorkspaceItemType.Notebook, notebook.CreatedAt, notebook.UpdatedAt);
    }
}
