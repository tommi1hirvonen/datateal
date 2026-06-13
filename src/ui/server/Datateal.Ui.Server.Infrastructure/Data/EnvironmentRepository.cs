using Datateal.Core.Environment;
using Datateal.Data;
using Datateal.Ui.Server.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Ui.Server.Infrastructure.Data;

internal class EnvironmentRepository(DatatealDbContext db) : IEnvironmentRepository
{
    // ── Environment Variables ───────────────────────────────────────────

    public async Task<IReadOnlyList<EnvironmentVariable>> GetVariablesAsync(Guid workspaceId, CancellationToken ct = default) =>
        await db.EnvironmentVariables
            .Where(v => v.WorkspaceId == workspaceId)
            .OrderBy(v => v.Key)
            .ToListAsync(ct);

    public Task<EnvironmentVariable?> GetVariableAsync(Guid workspaceId, Guid id, CancellationToken ct = default) =>
        db.EnvironmentVariables
            .Where(v => v.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<EnvironmentVariable> CreateVariableAsync(Guid workspaceId, string key, string value, string? description, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var variable = new EnvironmentVariable
        {
            Id = Guid.CreateVersion7(),
            Key = key,
            Value = value,
            Description = description,
            WorkspaceId = workspaceId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.EnvironmentVariables.Add(variable);
        await db.SaveChangesAsync(ct);
        return variable;
    }

    public async Task<EnvironmentVariable?> UpdateVariableAsync(Guid workspaceId, Guid id, string key, string value, string? description, CancellationToken ct = default)
    {
        var variable = await db.EnvironmentVariables
            .Where(v => v.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        if (variable is null) return null;

        variable.Key = key;
        variable.Value = value;
        variable.Description = description;
        variable.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return variable;
    }

    public async Task<bool> DeleteVariableAsync(Guid workspaceId, Guid id, CancellationToken ct = default)
    {
        var variable = await db.EnvironmentVariables
            .Where(v => v.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        if (variable is null) return false;

        db.EnvironmentVariables.Remove(variable);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<EnvironmentVariable>> GetVariablesByIdsAsync(Guid workspaceId, IReadOnlyList<Guid> ids, CancellationToken ct = default) =>
        await db.EnvironmentVariables
            .Where(v => v.WorkspaceId == workspaceId)
            .Where(v => ids.Contains(v.Id))
            .ToListAsync(ct);

    // ── Secrets ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Secret>> GetSecretsAsync(Guid workspaceId, CancellationToken ct = default) =>
        await db.Secrets
            .Where(s => s.WorkspaceId == workspaceId)
            .OrderBy(s => s.Key)
            .ToListAsync(ct);

    public Task<Secret?> GetSecretAsync(Guid workspaceId, Guid id, CancellationToken ct = default) =>
        db.Secrets
            .Where(s => s.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Secret> CreateSecretAsync(Guid workspaceId, string key, string encryptedValue, string? description, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var secret = new Secret
        {
            Id = Guid.CreateVersion7(),
            Key = key,
            EncryptedValue = encryptedValue,
            Description = description,
            WorkspaceId = workspaceId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Secrets.Add(secret);
        await db.SaveChangesAsync(ct);
        return secret;
    }

    public async Task<Secret?> UpdateSecretAsync(Guid workspaceId, Guid id, string key, string encryptedValue, string? description, CancellationToken ct = default)
    {
        var secret = await db.Secrets
            .Where(s => s.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (secret is null) return null;

        secret.Key = key;
        secret.EncryptedValue = encryptedValue;
        secret.Description = description;
        secret.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return secret;
    }

    public async Task<bool> DeleteSecretAsync(Guid workspaceId, Guid id, CancellationToken ct = default)
    {
        var secret = await db.Secrets
            .Where(s => s.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (secret is null) return false;

        db.Secrets.Remove(secret);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<Secret>> GetSecretsByIdsAsync(Guid workspaceId, IReadOnlyList<Guid> ids, CancellationToken ct = default) =>
        await db.Secrets
            .Where(s => s.WorkspaceId == workspaceId)
            .Where(s => ids.Contains(s.Id))
            .ToListAsync(ct);
}
