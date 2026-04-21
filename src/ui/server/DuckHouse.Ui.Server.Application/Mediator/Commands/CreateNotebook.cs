using DuckHouse.Core.Mediator;
using DuckHouse.Core.Workspace;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Server.Core.Workspace;
using DuckHouse.Ui.Shared.Workspace;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record CreateNotebookRequest(string Title, string Content, Guid? FolderId) : IRequest<WorkspaceItemSummary>;

internal class CreateNotebookHandler(IWorkspaceRepository repository) : IRequestHandler<CreateNotebookRequest, WorkspaceItemSummary>
{
    public async Task<WorkspaceItemSummary> Handle(CreateNotebookRequest request, CancellationToken cancellationToken)
    {
        WorkspaceNameValidationException.ValidateNoSlash(request.Title);
        if (await repository.WorkspaceItemTitleExistsAsync(request.Title, request.FolderId, cancellationToken: cancellationToken))
            throw new WorkspaceTitleConflictException(request.Title, request.FolderId is null ? "the root folder" : "this folder");

        var notebook = await repository.CreateNotebookAsync(request.Title, request.Content, request.FolderId, cancellationToken);
        return new WorkspaceItemSummary(notebook.Id, notebook.Title, notebook.FolderId, WorkspaceItemType.Notebook, notebook.CreatedAt, notebook.UpdatedAt);
    }
}
