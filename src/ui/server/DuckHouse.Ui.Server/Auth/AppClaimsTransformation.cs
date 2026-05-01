using System.Security.Claims;
using DuckHouse.Auth;
using DuckHouse.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DuckHouse.Ui.Server.Auth;

/// <summary>
/// Transforms OIDC claims into app-managed role claims by looking up the user
/// in the admin seed list and the application database.
/// </summary>
public class AppClaimsTransformation(
    DuckHouseDbContext dbContext,
    IOptions<AdminUsersOptions> adminOptions,
    ILogger<AppClaimsTransformation> logger) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
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
            AddRoleClaim(identity, DuckHouseRole.Admin);
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
        }

        return principal;
    }

    private static void AddRoleClaim(ClaimsIdentity identity, string role)
    {
        if (!identity.HasClaim(ClaimTypes.Role, role))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
    }
}
