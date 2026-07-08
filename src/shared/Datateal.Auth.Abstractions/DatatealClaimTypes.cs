namespace Datateal.Auth;

/// <summary>
/// Custom claim types issued by the application (beyond standard OIDC/role claims).
/// </summary>
public static class DatatealClaimTypes
{
    /// <summary>
    /// The acting user's stable application id (<see cref="System.Guid"/> form of
    /// <c>AppUser.Id</c>). Emitted by claims transformation when the principal resolves to a
    /// provisioned application user.
    /// </summary>
    public const string UserId = "datateal:user_id";

    /// <summary>
    /// Identifies how the principal authenticated. When present with the value
    /// <see cref="AuthMethodApiToken"/>, the principal was authenticated by an API token and
    /// its claims are authoritative — claims transformation must not augment them.
    /// </summary>
    public const string AuthMethod = "datateal:auth_method";

    /// <summary>Value of <see cref="AuthMethod"/> for API-token authenticated principals.</summary>
    public const string AuthMethodApiToken = "api_token";

    /// <summary>The id of the <c>ApiToken</c> used to authenticate the request (audit/telemetry).</summary>
    public const string TokenId = "datateal:token_id";
}

/// <summary>
/// Custom HTTP headers used between Datateal services.
/// </summary>
public static class DatatealHeaders
{
    /// <summary>
    /// Conveys the acting user's application id (<c>AppUser.Id</c>) from the UI server to the
    /// orchestrator over the service-to-service (API-key) boundary, so jobs can record a
    /// server-stamped effective owner. Never trusted from external clients.
    /// </summary>
    public const string ActingUser = "X-Datateal-Acting-User";
}
