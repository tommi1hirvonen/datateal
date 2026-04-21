using DuckHouse.Core.Mediator;
using DuckHouse.Core.Workspace;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Workspace;

namespace DuckHouse.Ui.Server.Application.Mediator.Queries;

public record SearchWorkspaceRequest(string Query) : IRequest<WorkspaceSearchResult>;

internal class SearchWorkspaceHandler(IWorkspaceRepository repository)
    : IRequestHandler<SearchWorkspaceRequest, WorkspaceSearchResult>
{
    public async Task<WorkspaceSearchResult> Handle(SearchWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var items = await repository.SearchItemsAsync(request.Query, cancellationToken);

        var notebooks = items
            .OfType<Notebook>()
            .Select(n => new NotebookSummary(n.Id, n.Title, n.FolderId, n.CreatedAt, n.UpdatedAt))
            .ToList();

        var queries = items
            .OfType<Query>()
            .Select(q => new QuerySummary(q.Id, q.Title, q.FolderId, q.CreatedAt, q.UpdatedAt))
            .ToList();

        return new WorkspaceSearchResult(notebooks, queries);
    }
}
