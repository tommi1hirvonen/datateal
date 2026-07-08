using Datateal.Core.ApiTokens;
using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.ApiTokens;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record CreateApiTokenCommand(
    string Name,
    ApiTokenScopeType ScopeType,
    Guid? WorkspaceId,
    List<string> Roles,
    DateTime? ValidTo,
    Guid? CreatedByUserId,
    Guid? ActingUserId = null) : IRequest<CreateApiTokenResponse>;

internal class CreateApiTokenHandler(
    IApiTokenRepository repository,
    IWorkspaceManagementRepository workspaceRepository)
    : IRequestHandler<CreateApiTokenCommand, CreateApiTokenResponse>
{
    public async Task<CreateApiTokenResponse> Handle(CreateApiTokenCommand request, CancellationToken cancellationToken)
    {
        var (value, prefix, hash) = ApiTokenGenerator.Generate();
        var now = DateTime.UtcNow;

        var token = new ApiToken
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            TokenPrefix = prefix,
            TokenHash = hash,
            ScopeType = request.ScopeType,
            WorkspaceId = request.ScopeType == ApiTokenScopeType.Workspace ? request.WorkspaceId : null,
            Roles = request.Roles,
            CreatedByUserId = request.CreatedByUserId,
            ActingUserId = request.ActingUserId,
            ValidFrom = now,
            ValidTo = request.ValidTo,
            IsRevoked = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await repository.CreateAsync(token, cancellationToken);

        string? workspaceName = null;
        if (token.WorkspaceId is { } workspaceId)
            workspaceName = (await workspaceRepository.GetAsync(workspaceId, cancellationToken))?.Name;

        return new CreateApiTokenResponse(ApiTokenDtoMapper.ToDto(token, workspaceName), value);
    }
}
