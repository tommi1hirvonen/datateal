namespace Datateal.Ui.Server.Auth;

/// <summary>
/// Constants for the Datateal API-token authentication scheme and the smart policy scheme that
/// routes each request to either the API-token scheme or the interactive (OIDC/cookie/dev) scheme.
/// </summary>
public static class ApiTokenAuthenticationDefaults
{
    /// <summary>The API-token authentication scheme name.</summary>
    public const string Scheme = "DatatealApiToken";

    /// <summary>
    /// The default policy scheme that forwards to <see cref="Scheme"/> when an API-token header is
    /// present, and to the interactive provider scheme otherwise.
    /// </summary>
    public const string SmartScheme = "DatatealSmartAuth";

    /// <summary>Dedicated header carrying the raw token (alternative to <c>Authorization: Bearer</c>).</summary>
    public const string HeaderName = "X-Datateal-Api-Token";
}
