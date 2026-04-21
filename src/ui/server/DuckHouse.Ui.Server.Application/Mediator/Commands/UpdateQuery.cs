using DuckHouse.Core.Mediator;
using DuckHouse.Core.Workspace;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Server.Core.Workspace;
using DuckHouse.Ui.Shared.Workspace;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record UpdateQueryRequest(Guid Id, string Title, string Content, Guid? FolderId) : IRequest<WorkspaceItemSummary?>;

internal class UpdateQueryHandler(IWorkspaceRepository repository) : IRequestHandler<UpdateQueryRequest, WorkspaceItemSummary?>
{
    public async Task<WorkspaceItemSummary?> Handle(UpdateQueryRequest request, CancellationToken cancellationToken)
    {
        WorkspaceNameValidationException.ValidateNoSlash(request.Title);
        if (await repository.WorkspaceItemTitleExistsAsync(request.Title, request.FolderId, excludeId: request.Id, cancellationToken: cancellationToken))
            throw new WorkspaceTitleConflictException(request.Title, request.FolderId is null ? "the root folder" : "this folder");

        var query = await repository.UpdateQueryAsync(request.Id, request.Title, request.Content, request.FolderId, cancellationToken);
        return query is null ? null : new WorkspaceItemSummary(query.Id, query.Title, query.FolderId, WorkspaceItemType.Query, query.CreatedAt, query.UpdatedAt);
    }
}
