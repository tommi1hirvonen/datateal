using Datateal.Core.Mediator;
using Datateal.Core.Workspace;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Server.Core.Workspace;
using Datateal.Ui.Shared.Workspace;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record UpdateQueryRequest(
    Guid WorkspaceId,
    Guid Id,
    string Title,
    string Content,
    Guid? FolderId,
    QueryLastResult? LastResult) : IRequest<WorkspaceItemSummary?>;

internal class UpdateQueryHandler(IWorkspaceRepository repository)
    : IRequestHandler<UpdateQueryRequest, WorkspaceItemSummary?>
{
    public async Task<WorkspaceItemSummary?> Handle(UpdateQueryRequest request, CancellationToken cancellationToken)
    {
        WorkspaceNameValidationException.ValidateNoSlash(request.Title);
        if (await repository.WorkspaceItemTitleExistsAsync(
            request.WorkspaceId,
            request.Title, 
            request.FolderId,
            excludeId: request.Id,
            cancellationToken: cancellationToken))
            throw new WorkspaceTitleConflictException(
                request.Title,
                request.FolderId is null ? "the root folder" : "this folder");

        var (status, durationMs, executedAt, resultJson) = CreateQueryHandler.SerializeResult(request.LastResult);
        var query = await repository.UpdateQueryAsync(
            request.WorkspaceId,
            request.Id,
            request.Title,
            request.Content,
            request.FolderId,
            status,
            durationMs,
            executedAt,
            resultJson,
            cancellationToken);
        return query is null ? null : new WorkspaceItemSummary(
            query.Id,
            query.Title,
            query.FolderId,
            WorkspaceItemType.Query,
            query.CreatedAt,
            query.UpdatedAt);
    }
}
