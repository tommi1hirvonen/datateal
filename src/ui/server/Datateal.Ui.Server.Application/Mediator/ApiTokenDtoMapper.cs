using Datateal.Core.ApiTokens;
using Datateal.Ui.Shared.ApiTokens;

namespace Datateal.Ui.Server.Application.Mediator;

internal static class ApiTokenDtoMapper
{
    internal static ApiTokenDto ToDto(ApiToken token, string? workspaceName) =>
        new(token.Id,
            token.Name,
            token.TokenPrefix,
            token.ScopeType.ToString(),
            token.WorkspaceId,
            workspaceName,
            token.Roles,
            token.CreatedByUserId,
            token.ValidFrom,
            token.ValidTo,
            token.IsRevoked,
            token.LastUsedAt,
            token.CreatedAt);
}
