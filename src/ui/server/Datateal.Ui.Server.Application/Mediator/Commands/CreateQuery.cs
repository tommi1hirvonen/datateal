using System.Text.Json;
using Datateal.Core.Kernels;
using Datateal.Core.Mediator;
using Datateal.Core.Workspace;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Server.Core.Workspace;
using Datateal.Ui.Shared.Workspace;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record CreateQueryRequest(Guid WorkspaceId, string Title, string Content, Guid? FolderId, QueryLastResult? LastResult) : IRequest<WorkspaceItemSummary>;

internal class CreateQueryHandler(IWorkspaceRepository repository) : IRequestHandler<CreateQueryRequest, WorkspaceItemSummary>
{
    public async Task<WorkspaceItemSummary> Handle(CreateQueryRequest request, CancellationToken cancellationToken)
    {
        WorkspaceNameValidationException.ValidateNoSlash(request.Title);
        if (await repository.WorkspaceItemTitleExistsAsync(request.WorkspaceId, request.Title, request.FolderId, cancellationToken: cancellationToken))
            throw new WorkspaceTitleConflictException(request.Title, request.FolderId is null ? "the root folder" : "this folder");

        var (status, durationMs, executedAt, resultJson) = SerializeResult(request.LastResult);
        var query = await repository.CreateQueryAsync(request.WorkspaceId, request.Title, request.Content, request.FolderId, status, durationMs, executedAt, resultJson, cancellationToken);
        return new WorkspaceItemSummary(query.Id, query.Title, query.FolderId, WorkspaceItemType.Query, query.CreatedAt, query.UpdatedAt);
    }

    internal static (string? status, double? durationMs, DateTime? executedAt, string? resultJson) SerializeResult(QueryLastResult? result)
    {
        if (result is null) return (null, null, null, null);
        string? resultJson = null;
        if (result.DataFrame is not null || result.Text is not null || result.Error is not null)
            resultJson = JsonSerializer.Serialize(new ResultPayload(result.DataFrame, result.Text, result.Error));
        return (result.Status, result.DurationMs, result.ExecutedAt, resultJson);
    }

    internal record ResultPayload(DataFrameOutput? DataFrame, string? Text, ErrorInfo? Error);
}
