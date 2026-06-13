using Datateal.Core.Mediator;
using Datateal.Core.Workspace;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Server.Core.Workspace;
using Datateal.Ui.Shared.Workspace;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record CreateNotebookRequest(Guid WorkspaceId, string Title, string Content, Guid? FolderId) : IRequest<WorkspaceItemSummary>;

internal class CreateNotebookHandler(IWorkspaceRepository repository) : IRequestHandler<CreateNotebookRequest, WorkspaceItemSummary>
{
    public async Task<WorkspaceItemSummary> Handle(CreateNotebookRequest request, CancellationToken cancellationToken)
    {
        WorkspaceNameValidationException.ValidateNoSlash(request.Title);
        if (await repository.WorkspaceItemTitleExistsAsync(request.WorkspaceId, request.Title, request.FolderId, cancellationToken: cancellationToken))
            throw new WorkspaceTitleConflictException(request.Title, request.FolderId is null ? "the root folder" : "this folder");

        var notebook = await repository.CreateNotebookAsync(request.WorkspaceId, request.Title, request.Content, request.FolderId, cancellationToken);
        return new WorkspaceItemSummary(notebook.Id, notebook.Title, notebook.FolderId, WorkspaceItemType.Notebook, notebook.CreatedAt, notebook.UpdatedAt);
    }
}
