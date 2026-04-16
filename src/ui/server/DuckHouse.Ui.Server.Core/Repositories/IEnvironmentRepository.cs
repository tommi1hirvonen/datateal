using DuckHouse.Core.Environment;

namespace DuckHouse.Ui.Server.Core.Repositories;

public interface IEnvironmentRepository
{
    // Environment variables
    Task<IReadOnlyList<EnvironmentVariable>> GetVariablesAsync(CancellationToken ct = default);
    Task<EnvironmentVariable?> GetVariableAsync(Guid id, CancellationToken ct = default);
    Task<EnvironmentVariable> CreateVariableAsync(string key, string value, string? description, CancellationToken ct = default);
    Task<EnvironmentVariable?> UpdateVariableAsync(Guid id, string key, string value, string? description, CancellationToken ct = default);
    Task<bool> DeleteVariableAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<EnvironmentVariable>> GetVariablesByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);

    // Secrets
    Task<IReadOnlyList<Secret>> GetSecretsAsync(CancellationToken ct = default);
    Task<Secret?> GetSecretAsync(Guid id, CancellationToken ct = default);
    Task<Secret> CreateSecretAsync(string key, string encryptedValue, string? description, CancellationToken ct = default);
    Task<Secret?> UpdateSecretAsync(Guid id, string key, string encryptedValue, string? description, CancellationToken ct = default);
    Task<bool> DeleteSecretAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Secret>> GetSecretsByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
}
