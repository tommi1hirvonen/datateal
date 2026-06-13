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
