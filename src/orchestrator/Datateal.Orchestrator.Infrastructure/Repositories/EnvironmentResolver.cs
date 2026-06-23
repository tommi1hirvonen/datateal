using Datateal.Data;
using Datateal.Orchestrator.Core.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Datateal.Orchestrator.Infrastructure.Repositories;

/// <summary>
/// Resolves environment variable and secret IDs by reading directly from the shared
/// database. Secrets are decrypted using the shared Data Protection key ring.
/// </summary>
internal class EnvironmentResolver(
    IServiceScopeFactory scopeFactory,
    IDataProtectionProvider dataProtectionProvider) : IEnvironmentResolver
{
    private const string DataProtectionPurpose = "Datateal.Secrets";

    public async Task<ResolvedEnvironmentEntries> ResolveAsync(
        IReadOnlyList<Guid>? environmentVariableIds,
        IReadOnlyList<Guid>? secretIds,
        CancellationToken ct = default)
    {
        var variables = new Dictionary<string, string>();
        var secrets = new Dictionary<string, string>();

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DatatealDbContext>();

        if (environmentVariableIds is { Count: > 0 })
        {
            var rows = await db.EnvironmentVariables
                .Where(v => environmentVariableIds.Contains(v.Id))
                .Select(v => new { v.Key, v.Value })
                .ToListAsync(ct);

            foreach (var row in rows)
                variables[row.Key] = row.Value;
        }

        if (secretIds is { Count: > 0 })
        {
            var protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);

            var rows = await db.Secrets
                .Where(s => secretIds.Contains(s.Id))
                .Select(s => new { s.Key, s.EncryptedValue })
                .ToListAsync(ct);

            foreach (var row in rows)
                secrets[row.Key] = protector.Unprotect(row.EncryptedValue);
        }

        return new ResolvedEnvironmentEntries(variables, secrets);
    }
}
