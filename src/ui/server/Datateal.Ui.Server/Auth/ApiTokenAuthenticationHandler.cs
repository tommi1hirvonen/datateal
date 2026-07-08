using System.Security.Claims;
using System.Text.Encodings.Web;
using Datateal.Auth;
using Datateal.Core.ApiTokens;
using Datateal.Ui.Shared.Workspaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Datateal.Ui.Server.Auth;

/// <summary>
/// Authenticates requests bearing a Datateal API token (<c>Authorization: Bearer dtl_...</c> or
/// the <c>X-Datateal-Api-Token</c> header). On success it projects the token into the same claim
/// shapes an interactive user carries, so all existing authorization policies apply unchanged:
/// tenant roles as <see cref="ClaimTypes.Role"/> (admin tokens) or per-workspace role claims for
/// a single workspace (workspace tokens). Isolation is enforced because a workspace token emits
/// claims for its one workspace only.
/// </summary>
public sealed class ApiTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiTokenAuthenticator authenticator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ExtractToken(Request);
        if (token is null)
            return AuthenticateResult.NoResult();

        var apiToken = await authenticator.ValidateAsync(token, Context.RequestAborted);
        if (apiToken is null)
            return AuthenticateResult.Fail("Invalid, expired, or revoked API token.");

        var identity = new ClaimsIdentity(ApiTokenAuthenticationDefaults.Scheme, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.Name, apiToken.Name));
        identity.AddClaim(new Claim(DatatealClaimTypes.AuthMethod, DatatealClaimTypes.AuthMethodApiToken));
        identity.AddClaim(new Claim(DatatealClaimTypes.TokenId, apiToken.Id.ToString()));

        if (apiToken.ActingUserId is { } actingId)
            identity.AddClaim(new Claim(DatatealClaimTypes.UserId, actingId.ToString()));
        else if (apiToken.CreatedByUserId is { } ownerId)
            identity.AddClaim(new Claim(DatatealClaimTypes.UserId, ownerId.ToString()));

        switch (apiToken.ScopeType)
        {
            case ApiTokenScopeType.Admin:
                foreach (var role in apiToken.Roles)
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                break;

            case ApiTokenScopeType.Workspace when apiToken.WorkspaceId is { } workspaceId:
                foreach (var role in apiToken.Roles)
                    identity.AddClaim(WorkspaceRoleClaims.CreateClaim(workspaceId, role));
                break;
        }

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiTokenAuthenticationDefaults.Scheme);
        return AuthenticateResult.Success(ticket);
    }

    /// <summary>API clients get a 401 (not an interactive redirect) when a token is missing/invalid.</summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    internal static string? ExtractToken(HttpRequest request)
    {
        if (request.Headers.TryGetValue(ApiTokenAuthenticationDefaults.HeaderName, out var headerValues))
        {
            var value = headerValues.ToString();
            if (ApiTokenGenerator.LooksLikeToken(value))
                return value;
        }

        var authorization = request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var value = authorization["Bearer ".Length..].Trim();
            if (ApiTokenGenerator.LooksLikeToken(value))
                return value;
        }

        return null;
    }
}
