using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.ApiTokens;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record GetApiTokensRequest : IRequest<IReadOnlyList<ApiTokenDto>>;

internal class GetApiTokensHandler(
    IApiTokenRepository repository,
    IWorkspaceManagementRepository workspaceRepository)
    : IRequestHandler<GetApiTokensRequest, IReadOnlyList<ApiTokenDto>>
{
    public async Task<IReadOnlyList<ApiTokenDto>> Handle(GetApiTokensRequest request, CancellationToken cancellationToken)
    {
        var tokens = await repository.GetAllAsync(ct: cancellationToken);

        var workspaceIds = tokens
            .Where(t => t.WorkspaceId is not null)
            .Select(t => t.WorkspaceId!.Value)
            .Distinct()
            .ToList();

        var workspaceNames = workspaceIds.Count == 0
            ? []
            : (await workspaceRepository.GetByIdsAsync(workspaceIds, cancellationToken))
                .ToDictionary(w => w.Id, w => w.Name);

        return tokens
            .Select(t => ApiTokenDtoMapper.ToDto(
                t,
                t.WorkspaceId is { } id && workspaceNames.TryGetValue(id, out var name) ? name : null))
            .ToList();
    }
}
