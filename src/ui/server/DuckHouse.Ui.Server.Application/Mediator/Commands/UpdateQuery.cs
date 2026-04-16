using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Server.Core.Workspace;
using DuckHouse.Ui.Shared.Workspace;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record UpdateQueryRequest(Guid Id, string Title, string Content, Guid? FolderId) : IRequest<QuerySummary?>;

internal class UpdateQueryHandler(IWorkspaceRepository repository) : IRequestHandler<UpdateQueryRequest, QuerySummary?>
{
    public async Task<QuerySummary?> Handle(UpdateQueryRequest request, CancellationToken cancellationToken)
    {
        if (await repository.WorkspaceItemTitleExistsAsync(request.Title, request.FolderId, excludeId: request.Id, cancellationToken: cancellationToken))
            throw new WorkspaceTitleConflictException(request.Title, request.FolderId is null ? "the root folder" : "this folder");

        var query = await repository.UpdateQueryAsync(request.Id, request.Title, request.Content, request.FolderId, cancellationToken);
        return query is null ? null : new QuerySummary(query.Id, query.Title, query.FolderId, query.CreatedAt, query.UpdatedAt);
    }
}
