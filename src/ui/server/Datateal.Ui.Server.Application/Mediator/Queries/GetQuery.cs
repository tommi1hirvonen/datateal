using System.Text.Json;
using Datateal.Core.Kernels;
using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Workspace;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record GetQueryRequest(Guid WorkspaceId, Guid Id) : IRequest<QueryDetail?>;

internal class GetQueryHandler(IWorkspaceRepository repository) : IRequestHandler<GetQueryRequest, QueryDetail?>
{
    public async Task<QueryDetail?> Handle(GetQueryRequest request, CancellationToken cancellationToken)
    {
        var query = await repository.GetQueryAsync(request.WorkspaceId, request.Id, cancellationToken);
        if (query is null) return null;

        QueryLastResult? lastResult = null;
        if (query.LastResultStatus is not null && query.LastExecutedAt.HasValue)
        {
            ResultPayload? payload = null;
            if (query.LastResultJson is not null)
            {
                try { payload = JsonSerializer.Deserialize<ResultPayload>(query.LastResultJson); }
                catch { /* ignore corrupt data */ }
            }

            lastResult = new QueryLastResult(
                query.LastResultStatus,
                query.LastExecutedAt.Value,
                query.LastDurationMs ?? 0,
                payload?.DataFrame,
                payload?.Text,
                payload?.Error);
        }

        return new QueryDetail(query.Id, query.Title, query.FolderId, query.CreatedAt, query.UpdatedAt, query.Content, lastResult, query.CatalogNames);
    }

    internal record ResultPayload(DataFrameOutput? DataFrame, string? Text, ErrorInfo? Error);
}
