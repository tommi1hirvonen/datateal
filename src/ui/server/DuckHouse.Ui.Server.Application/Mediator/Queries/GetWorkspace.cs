using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Core.Workspace;
using DuckHouse.Ui.Shared.Workspace;

namespace DuckHouse.Ui.Server.Application.Mediator.Queries;

public record GetWorkspaceRequest(Guid? FolderId = null) : IRequest<WorkspaceListing>;

internal class GetWorkspaceHandler(IWorkspaceRepository repository) : IRequestHandler<GetWorkspaceRequest, WorkspaceListing>
{
    public async Task<WorkspaceListing> Handle(GetWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var folders = await repository.GetFoldersInAsync(request.FolderId, cancellationToken);
        var items = await repository.GetItemsInAsync(request.FolderId, cancellationToken);

        var folderSummaries = folders
            .Select(f => new FolderSummary(f.Id, f.Name, f.ParentId, f.CreatedAt))
            .ToList();

        var notebookSummaries = items
            .OfType<Notebook>()
            .Select(n => new NotebookSummary(n.Id, n.Title, n.FolderId, n.CreatedAt, n.UpdatedAt))
            .ToList();

        var querySummaries = items
            .OfType<Query>()
            .Select(q => new QuerySummary(q.Id, q.Title, q.FolderId, q.CreatedAt, q.UpdatedAt))
            .ToList();

        return new WorkspaceListing(folderSummaries, notebookSummaries, querySummaries);
    }
}
