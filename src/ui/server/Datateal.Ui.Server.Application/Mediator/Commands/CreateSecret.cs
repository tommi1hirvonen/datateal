using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Environment;
using Microsoft.AspNetCore.DataProtection;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record CreateSecretRequest(Guid WorkspaceId, string Key, string Value, string? Description)
    : IRequest<SecretDto>;

internal class CreateSecretHandler(IEnvironmentRepository repository, IDataProtectionProvider dataProtection)
    : IRequestHandler<CreateSecretRequest, SecretDto>
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("Datateal.Secrets");

    public async Task<SecretDto> Handle(CreateSecretRequest request, CancellationToken cancellationToken)
    {
        var encryptedValue = _protector.Protect(request.Value);
        var secret = await repository.CreateSecretAsync(request.WorkspaceId, request.Key, encryptedValue, request.Description, cancellationToken);
        return new SecretDto(secret.Id, secret.Key, secret.Description, secret.CreatedAt, secret.UpdatedAt);
    }
}
