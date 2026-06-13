using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Environment;
using Microsoft.AspNetCore.DataProtection;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record UpdateSecretRequest(Guid WorkspaceId, Guid Id, string Key, string? Value, string? Description)
    : IRequest<SecretDto?>;

internal class UpdateSecretHandler(IEnvironmentRepository repository, IDataProtectionProvider dataProtection)
    : IRequestHandler<UpdateSecretRequest, SecretDto?>
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("Datateal.Secrets");

    public async Task<SecretDto?> Handle(UpdateSecretRequest request, CancellationToken cancellationToken)
    {
        // If value is null, keep the existing encrypted value
        string encryptedValue;
        if (request.Value is not null)
        {
            encryptedValue = _protector.Protect(request.Value);
        }
        else
        {
            var existing = await repository.GetSecretAsync(request.WorkspaceId, request.Id, cancellationToken);
            if (existing is null) return null;
            encryptedValue = existing.EncryptedValue;
        }

        var secret = await repository.UpdateSecretAsync(request.WorkspaceId, request.Id, request.Key, encryptedValue, request.Description, cancellationToken);
        if (secret is null) return null;
        return new SecretDto(secret.Id, secret.Key, secret.Description, secret.CreatedAt, secret.UpdatedAt);
    }
}
