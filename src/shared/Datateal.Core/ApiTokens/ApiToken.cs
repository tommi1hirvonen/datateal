namespace Datateal.Core.ApiTokens;

/// <summary>
/// A non-interactive API credential used for programmatic and CI/CD access. Tokens are
/// projected into the same claim shapes an interactive user carries, so every existing
/// authorization policy accepts them. The secret value is never stored — only a SHA-256 hash
/// (<see cref="TokenHash"/>) plus a short lookup/display <see cref="TokenPrefix"/>.
/// </summary>
public class ApiToken
{
    public Guid Id { get; set; }

    /// <summary>Human-friendly label for management and auditing.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// First characters of the token (including the <c>dtl_</c> prefix). Stored for fast lookup
    /// and for display in management UIs. Not a secret on its own.
    /// </summary>
    public required string TokenPrefix { get; set; }

    /// <summary>SHA-256 hash (hex) of the full token string. The plaintext is never persisted.</summary>
    public required string TokenHash { get; set; }

    public ApiTokenScopeType ScopeType { get; set; }

    /// <summary>Owning workspace. Required for <see cref="ApiTokenScopeType.Workspace"/>; null for admin tokens.</summary>
    public Guid? WorkspaceId { get; set; }

    /// <summary>
    /// Roles granted by the token. For admin tokens these are tenant-global roles; for workspace
    /// tokens these are per-workspace roles applied to <see cref="WorkspaceId"/> only. Stored as a
    /// JSON array (EF Core primitive collection).
    /// </summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>
    /// The user who created the token. Used as the effective/acting user for the token's requests
    /// (job ownership, catalog access checks) when <see cref="ActingUserId"/> is not set.
    /// References <c>AppUser.Id</c>.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// Optional override for the acting identity of this token. When set, all requests
    /// authenticated by this token use this identity (rather than <see cref="CreatedByUserId"/>)
    /// for job ownership and catalog access checks. Typically points to a
    /// <see cref="Datateal.Core.Users.ServiceAccount"/> to decouple automation pipelines
    /// from the creating user's personal permissions. References <c>AppUser.Id</c>.
    /// </summary>
    public Guid? ActingUserId { get; set; }

    public DateTime ValidFrom { get; set; }

    /// <summary>Expiry instant. <c>null</c> = non-expiring (but still revocable).</summary>
    public DateTime? ValidTo { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>Returns true if the token is currently usable at <paramref name="now"/>.</summary>
    public bool IsActive(DateTime now) =>
        !IsRevoked && ValidFrom <= now && (ValidTo is null || ValidTo > now);
}
