using System.Security.Claims;
using Datateal.Auth;
using Datateal.Auth.Dev;
using Datateal.Data;
using Datateal.Ui.Shared.Workspaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Datateal.Ui.Server.Auth;

/// <summary>
/// Transforms OIDC claims into app-managed role claims by looking up the user
/// in the admin seed list and the application database.
/// </summary>
public class AppClaimsTransformation(
    DatatealDbContext dbContext,
    IOptions<AdminUsersOptions> adminOptions,
    ILogger<AppClaimsTransformation> logger) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return principal;

        // When the dev auth provider has explicitly configured roles, they were already
        // added by DevAuthenticationHandler. Require both the dev scheme name AND the
        // override marker so this fast-path can never be triggered by a claim in a
        // production OIDC token (e.g. from Entra ID), regardless of its content.
        if (identity.AuthenticationType == DevAuthenticationOptions.SchemeName
            && identity.HasClaim(DevAuthenticationOptions.RolesOverrideClaim, "true"))
            return principal;

        var externalId = identity.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? identity.FindFirst("oid")?.Value;
        var email = identity.FindFirst("preferred_username")?.Value
            ?? identity.FindFirst(ClaimTypes.Email)?.Value
            ?? identity.FindFirst("email")?.Value;

        if (email is null && externalId is null)
        {
            logger.LogWarning("Authenticated user has no email or external ID claims.");
            return principal;
        }

        // Admin seed list — these users get the Admin role regardless of DB state
        var isAdminSeed = email is not null &&
            adminOptions.Value.AdminUsers.Contains(email, StringComparer.OrdinalIgnoreCase);

        if (isAdminSeed)
        {
            AddRoleClaim(identity, DatatealRole.Admin);
        }

        // Look up user in database by ExternalId first, then email.
        // Use AsNoTracking so this read does not pollute the scoped DbContext's change
        // tracker, which could interfere with later repository operations in the same request.
        var appUser = externalId is not null
            ? await dbContext.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.ExternalId == externalId)
            : null;

        appUser ??= email is not null
            ? await dbContext.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email)
            : null;

        if (appUser is not null)
        {
            if (!appUser.IsEnabled)
            {
                logger.LogInformation("User {Email} is disabled.", appUser.Email);
                return principal;
            }

            // Capture external ID on first login for stable future lookups.
            // Use ExecuteUpdateAsync to avoid tracking a stale entity.
            if (appUser.ExternalId is null && externalId is not null)
            {
                await dbContext.AppUsers
                    .Where(u => u.Id == appUser.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.ExternalId, externalId)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
            }

            foreach (var role in appUser.Roles)
            {
                AddRoleClaim(identity, role);
            }

            // Emit a stable application user id claim so downstream components (e.g. the
            // orchestrator proxy) can identify the acting user without an extra database lookup.
            if (!identity.HasClaim(DatatealClaimTypes.UserId, appUser.Id.ToString()))
            {
                identity.AddClaim(new Claim(DatatealClaimTypes.UserId, appUser.Id.ToString()));
            }

            // Per-workspace roles are emitted as custom claims so a single principal
            // carries roles for every workspace it belongs to, evaluated against the
            // active workspace at authorization time.
            var memberships = await dbContext.WorkspaceMemberships
                .AsNoTracking()
                .Where(m => m.UserId == appUser.Id)
                .Select(m => new { m.WorkspaceId, m.Roles })
                .ToListAsync();

            foreach (var membership in memberships)
            {
                foreach (var role in membership.Roles)
                {
                    AddWorkspaceRoleClaim(identity, membership.WorkspaceId, role);
                }
            }
        }

        return principal;
    }

    private static void AddWorkspaceRoleClaim(ClaimsIdentity identity, Guid workspaceId, string role)
    {
        var value = WorkspaceRoleClaims.FormatValue(workspaceId, role);
        if (!identity.HasClaim(WorkspaceRoleClaims.ClaimType, value))
        {
            identity.AddClaim(new Claim(WorkspaceRoleClaims.ClaimType, value));
        }
    }

    private static void AddRoleClaim(ClaimsIdentity identity, string role)
    {
        if (!identity.HasClaim(ClaimTypes.Role, role))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
    }
}
