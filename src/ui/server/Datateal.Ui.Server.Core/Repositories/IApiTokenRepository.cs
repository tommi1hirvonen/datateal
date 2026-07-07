using Datateal.Core.ApiTokens;

namespace Datateal.Ui.Server.Core.Repositories;

public interface IApiTokenRepository
{
    /// <summary>All tokens, newest first. Optionally filtered to a single workspace's tokens.</summary>
    Task<IReadOnlyList<ApiToken>> GetAllAsync(Guid? workspaceId = null, CancellationToken ct = default);

    Task<ApiToken?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Candidate tokens sharing a stored prefix. Used by token validation.</summary>
    Task<IReadOnlyList<ApiToken>> GetByPrefixAsync(string prefix, CancellationToken ct = default);

    Task<ApiToken> CreateAsync(ApiToken token, CancellationToken ct = default);

    /// <summary>Marks a token revoked. Returns false if not found.</summary>
    Task<bool> RevokeAsync(Guid id, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Records the last-used timestamp without tracking the entity.</summary>
    Task TouchLastUsedAsync(Guid id, DateTime usedAt, CancellationToken ct = default);
}
