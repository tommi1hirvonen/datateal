using DuckHouse.Core.Mediator;
using DuckHouse.Core.Workspace;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Server.Core.Workspace;
using DuckHouse.Ui.Shared.Workspace;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record CreateQueryRequest(string Title, string Content, Guid? FolderId) : IRequest<WorkspaceItemSummary>;

internal class CreateQueryHandler(IWorkspaceRepository repository) : IRequestHandler<CreateQueryRequest, WorkspaceItemSummary>
{
    public async Task<WorkspaceItemSummary> Handle(CreateQueryRequest request, CancellationToken cancellationToken)
    {
        WorkspaceNameValidationException.ValidateNoSlash(request.Title);
        if (await repository.WorkspaceItemTitleExistsAsync(request.Title, request.FolderId, cancellationToken: cancellationToken))
            throw new WorkspaceTitleConflictException(request.Title, request.FolderId is null ? "the root folder" : "this folder");

        var query = await repository.CreateQueryAsync(request.Title, request.Content, request.FolderId, cancellationToken);
        return new WorkspaceItemSummary(query.Id, query.Title, query.FolderId, WorkspaceItemType.Query, query.CreatedAt, query.UpdatedAt);
    }
}
