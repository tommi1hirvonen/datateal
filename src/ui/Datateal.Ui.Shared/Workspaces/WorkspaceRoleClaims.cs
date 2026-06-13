using System.Security.Claims;

namespace Datateal.Ui.Shared.Workspaces;

/// <summary>
/// Helpers for encoding and reading per-workspace role claims. Per-workspace roles are
/// emitted as claims of type <see cref="ClaimType"/> with the value
/// <c>{workspaceId:N}:{role}</c> so a single principal can carry roles for every
/// workspace it belongs to without re-issuing the auth state on workspace switch.
/// </summary>
public static class WorkspaceRoleClaims
{
    public const string ClaimType = "ws_role";

    public const string HeaderName = "X-Datateal-Workspace";

    public static string FormatValue(Guid workspaceId, string role) => $"{workspaceId:N}:{role}";

    public static Claim CreateClaim(Guid workspaceId, string role) => new(ClaimType, FormatValue(workspaceId, role));

    /// <summary>
    /// Returns the set of per-workspace roles the principal holds in the given workspace.
    /// </summary>
    public static IEnumerable<string> GetRoles(ClaimsPrincipal principal, Guid workspaceId)
    {
        var prefix = $"{workspaceId:N}:";
        foreach (var claim in principal.FindAll(ClaimType))
        {
            if (claim.Value.StartsWith(prefix, StringComparison.Ordinal))
                yield return claim.Value[prefix.Length..];
        }
    }

    /// <summary>
    /// Returns every workspace id the principal holds at least one per-workspace role in.
    /// </summary>
    public static IEnumerable<Guid> GetWorkspaceIds(ClaimsPrincipal principal)
    {
        foreach (var claim in principal.FindAll(ClaimType))
        {
            var sep = claim.Value.IndexOf(':');
            if (sep > 0 && Guid.TryParseExact(claim.Value[..sep], "N", out var id))
                yield return id;
        }
    }
}
