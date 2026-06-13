using Datateal.Core.Environment;
using Datateal.Data;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Ui.Server.Infrastructure.Data;

internal class EnvironmentRepository(DatatealDbContext db, IActiveWorkspaceAccessor activeWorkspace) : IEnvironmentRepository
{
    private Guid WorkspaceId => activeWorkspace.ActiveWorkspaceId
        ?? throw new InvalidOperationException("No active workspace is in scope for this request.");

    private IQueryable<EnvironmentVariable> Variables => db.EnvironmentVariables.Where(v => v.WorkspaceId == WorkspaceId);
    private IQueryable<Secret> SecretsScoped => db.Secrets.Where(s => s.WorkspaceId == WorkspaceId);

    // ── Environment Variables ───────────────────────────────────────────

    public async Task<IReadOnlyList<EnvironmentVariable>> GetVariablesAsync(CancellationToken ct = default) =>
        await Variables.OrderBy(v => v.Key).ToListAsync(ct);

    public Task<EnvironmentVariable?> GetVariableAsync(Guid id, CancellationToken ct = default) =>
        Variables.FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<EnvironmentVariable> CreateVariableAsync(string key, string value, string? description, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var variable = new EnvironmentVariable
        {
            Id = Guid.CreateVersion7(),
            Key = key,
            Value = value,
            Description = description,
            WorkspaceId = WorkspaceId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.EnvironmentVariables.Add(variable);
        await db.SaveChangesAsync(ct);
        return variable;
    }

    public async Task<EnvironmentVariable?> UpdateVariableAsync(Guid id, string key, string value, string? description, CancellationToken ct = default)
    {
        var variable = await Variables.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (variable is null) return null;

        variable.Key = key;
        variable.Value = value;
        variable.Description = description;
        variable.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return variable;
    }

    public async Task<bool> DeleteVariableAsync(Guid id, CancellationToken ct = default)
    {
        var variable = await Variables.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (variable is null) return false;

        db.EnvironmentVariables.Remove(variable);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<EnvironmentVariable>> GetVariablesByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) =>
        await Variables.Where(v => ids.Contains(v.Id)).ToListAsync(ct);

    // ── Secrets ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Secret>> GetSecretsAsync(CancellationToken ct = default) =>
        await SecretsScoped.OrderBy(s => s.Key).ToListAsync(ct);

    public Task<Secret?> GetSecretAsync(Guid id, CancellationToken ct = default) =>
        SecretsScoped.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Secret> CreateSecretAsync(string key, string encryptedValue, string? description, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var secret = new Secret
        {
            Id = Guid.CreateVersion7(),
            Key = key,
            EncryptedValue = encryptedValue,
            Description = description,
            WorkspaceId = WorkspaceId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Secrets.Add(secret);
        await db.SaveChangesAsync(ct);
        return secret;
    }

    public async Task<Secret?> UpdateSecretAsync(Guid id, string key, string encryptedValue, string? description, CancellationToken ct = default)
    {
        var secret = await SecretsScoped.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (secret is null) return null;

        secret.Key = key;
        secret.EncryptedValue = encryptedValue;
        secret.Description = description;
        secret.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return secret;
    }

    public async Task<bool> DeleteSecretAsync(Guid id, CancellationToken ct = default)
    {
        var secret = await SecretsScoped.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (secret is null) return false;

        db.Secrets.Remove(secret);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<Secret>> GetSecretsByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) =>
        await SecretsScoped.Where(s => ids.Contains(s.Id)).ToListAsync(ct);
}
