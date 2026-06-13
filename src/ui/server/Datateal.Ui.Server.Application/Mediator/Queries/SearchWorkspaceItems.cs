using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Workspace;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record SearchWorkspaceRequest(Guid WorkspaceId, string Query) : IRequest<WorkspaceSearchResult>;

internal class SearchWorkspaceHandler(IWorkspaceRepository repository)
    : IRequestHandler<SearchWorkspaceRequest, WorkspaceSearchResult>
{
    public async Task<WorkspaceSearchResult> Handle(SearchWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var items = await repository.SearchItemsAsync(request.WorkspaceId, request.Query, cancellationToken);

        var summaries = items
            .Select(h => new WorkspaceItemSummary(h.Id, h.Title, h.FolderId, h.ItemType, h.CreatedAt, h.UpdatedAt))
            .ToList();

        return new WorkspaceSearchResult(summaries);
    }
}
