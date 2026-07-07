using Datateal.Core.ApiTokens;
using Datateal.Ui.Server.Core.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace Datateal.Ui.Server.Auth;

/// <summary>
/// Validates raw API tokens against the database with a short-lived in-memory cache. Never logs
/// token material. Records <c>LastUsedAt</c> on cache-miss validations (throttled by the cache).
/// </summary>
public interface IApiTokenAuthenticator
{
    Task<ApiToken?> ValidateAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Removes any cached entry for the given token ID. Call after revoking or deleting a token
    /// so that the change takes effect immediately rather than after the cache window expires.
    /// </summary>
    void Evict(Guid id);
}

internal sealed class ApiTokenAuthenticator(
    IApiTokenRepository repository,
    IMemoryCache cache) : IApiTokenAuthenticator
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<ApiToken?> ValidateAsync(string token, CancellationToken ct = default)
    {
        if (!ApiTokenGenerator.LooksLikeToken(token))
            return null;

        var hash = ApiTokenGenerator.ComputeHash(token);
        var cacheKey = "apitoken:" + hash;
        var now = DateTime.UtcNow;

        if (cache.TryGetValue<ApiToken>(cacheKey, out var cached) && cached is not null)
            return cached.IsActive(now) ? cached : null;

        // Cache miss — look up by prefix, then constant-time compare the full hash.
        var prefix = ApiTokenGenerator.GetPrefix(token);
        var candidates = await repository.GetByPrefixAsync(prefix, ct);

        ApiToken? match = null;
        foreach (var candidate in candidates)
        {
            if (ApiTokenGenerator.HashesEqual(candidate.TokenHash, hash))
            {
                match = candidate;
                break;
            }
        }

        if (match is null || !match.IsActive(now))
            return null;

        // Cache the validated token and store a reverse id→hash mapping so Evict() can find
        // the cache entry by token ID (used when a token is revoked or deleted).
        cache.Set(cacheKey, match, CacheDuration);
        cache.Set(ReverseKey(match.Id), hash, CacheDuration);
        try
        {
            await repository.TouchLastUsedAsync(match.Id, now, ct);
        }
        catch
        {
            // Usage tracking is best-effort and must never block authentication.
        }

        return match;
    }

    public void Evict(Guid id)
    {
        var reverseKey = ReverseKey(id);
        if (cache.TryGetValue<string>(reverseKey, out var hash) && hash is not null)
            cache.Remove("apitoken:" + hash);
        cache.Remove(reverseKey);
    }

    private static string ReverseKey(Guid id) => "apitoken:id:" + id;
}
