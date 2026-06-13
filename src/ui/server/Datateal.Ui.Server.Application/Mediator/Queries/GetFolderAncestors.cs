using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Workspace;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record GetFolderAncestorsRequest(Guid WorkspaceId, Guid FolderId) : IRequest<IReadOnlyList<FolderSummary>>;

internal class GetFolderAncestorsHandler(IWorkspaceRepository repository)
    : IRequestHandler<GetFolderAncestorsRequest, IReadOnlyList<FolderSummary>>
{
    public async Task<IReadOnlyList<FolderSummary>> Handle(GetFolderAncestorsRequest request, CancellationToken cancellationToken)
    {
        var ancestors = await repository.GetFolderAncestorsAsync(request.WorkspaceId, request.FolderId, cancellationToken);
        return ancestors.Select(f => new FolderSummary(f.Id, f.Name, f.ParentId, f.CreatedAt)).ToList();
    }
}
