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

        // Cache the validated token and record usage (throttled to once per cache window).
        cache.Set(cacheKey, match, CacheDuration);
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
}
