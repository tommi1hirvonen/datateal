using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Workspace;

namespace DuckHouse.Ui.Server.Application.Mediator.Queries;

public record GetNotebookRequest(Guid Id) : IRequest<NotebookDetail?>;

internal class GetNotebookHandler(IWorkspaceRepository repository) : IRequestHandler<GetNotebookRequest, NotebookDetail?>
{
    public async Task<NotebookDetail?> Handle(GetNotebookRequest request, CancellationToken cancellationToken)
    {
        var notebook = await repository.GetNotebookAsync(request.Id, cancellationToken);
        return notebook is null
            ? null
            : new NotebookDetail(notebook.Id, notebook.Title, notebook.FolderId, notebook.CreatedAt, notebook.UpdatedAt, notebook.Content, notebook.CatalogNames);
    }
}
