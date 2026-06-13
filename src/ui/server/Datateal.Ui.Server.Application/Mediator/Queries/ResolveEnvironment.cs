using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Environment;
using Microsoft.AspNetCore.DataProtection;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

/// <summary>
/// Resolves environment variable and secret IDs into plaintext key-value pairs.
/// Used internally during node creation.
/// </summary>
public record ResolveEnvironmentRequest(
    Guid WorkspaceId,
    IReadOnlyList<Guid>? EnvironmentVariableIds,
    IReadOnlyList<Guid>? SecretIds) : IRequest<ResolvedEnvironment>;

internal class ResolveEnvironmentHandler(IEnvironmentRepository repository, IDataProtectionProvider dataProtection)
    : IRequestHandler<ResolveEnvironmentRequest, ResolvedEnvironment>
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("Datateal.Secrets");

    public async Task<ResolvedEnvironment> Handle(ResolveEnvironmentRequest request, CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, string>();
        var secrets = new Dictionary<string, string>();

        if (request.EnvironmentVariableIds is { Count: > 0 } varIds)
        {
            var envVars = await repository.GetVariablesByIdsAsync(request.WorkspaceId, varIds, cancellationToken);
            foreach (var v in envVars)
                variables[v.Key] = v.Value;
        }

        if (request.SecretIds is { Count: > 0 } secretIds)
        {
            var secretEntities = await repository.GetSecretsByIdsAsync(request.WorkspaceId, secretIds, cancellationToken);
            foreach (var s in secretEntities)
                secrets[s.Key] = _protector.Unprotect(s.EncryptedValue);
        }

        return new ResolvedEnvironment(variables, secrets);
    }
}
