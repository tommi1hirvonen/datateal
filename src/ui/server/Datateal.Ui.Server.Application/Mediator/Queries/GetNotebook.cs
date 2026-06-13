using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Workspace;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record GetNotebookRequest(Guid WorkspaceId, Guid Id) : IRequest<NotebookDetail?>;

internal class GetNotebookHandler(IWorkspaceRepository repository) : IRequestHandler<GetNotebookRequest, NotebookDetail?>
{
    public async Task<NotebookDetail?> Handle(GetNotebookRequest request, CancellationToken cancellationToken)
    {
        var notebook = await repository.GetNotebookAsync(request.WorkspaceId, request.Id, cancellationToken);
        return notebook is null
            ? null
            : new NotebookDetail(notebook.Id, notebook.Title, notebook.FolderId, notebook.CreatedAt, notebook.UpdatedAt, notebook.Content, notebook.CatalogNames);
    }
}
