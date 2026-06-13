using Datateal.Core.Environment;

namespace Datateal.Ui.Server.Core.Repositories;

public interface IEnvironmentRepository
{
    // Environment variables
    Task<IReadOnlyList<EnvironmentVariable>> GetVariablesAsync(Guid workspaceId, CancellationToken ct = default);
    Task<EnvironmentVariable?> GetVariableAsync(Guid workspaceId, Guid id, CancellationToken ct = default);
    Task<EnvironmentVariable> CreateVariableAsync(Guid workspaceId, string key, string value, string? description, CancellationToken ct = default);
    Task<EnvironmentVariable?> UpdateVariableAsync(Guid workspaceId, Guid id, string key, string value, string? description, CancellationToken ct = default);
    Task<bool> DeleteVariableAsync(Guid workspaceId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<EnvironmentVariable>> GetVariablesByIdsAsync(Guid workspaceId, IReadOnlyList<Guid> ids, CancellationToken ct = default);

    // Secrets
    Task<IReadOnlyList<Secret>> GetSecretsAsync(Guid workspaceId, CancellationToken ct = default);
    Task<Secret?> GetSecretAsync(Guid workspaceId, Guid id, CancellationToken ct = default);
    Task<Secret> CreateSecretAsync(Guid workspaceId, string key, string encryptedValue, string? description, CancellationToken ct = default);
    Task<Secret?> UpdateSecretAsync(Guid workspaceId, Guid id, string key, string encryptedValue, string? description, CancellationToken ct = default);
    Task<bool> DeleteSecretAsync(Guid workspaceId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Secret>> GetSecretsByIdsAsync(Guid workspaceId, IReadOnlyList<Guid> ids, CancellationToken ct = default);
}
