using DuckHouse.Core.Mediator;
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

        var summaries = items
            .Select(h => new WorkspaceItemSummary(h.Id, h.Title, h.FolderId, h.ItemType, h.CreatedAt, h.UpdatedAt))
            .ToList();

        return new WorkspaceSearchResult(summaries);
    }
}
