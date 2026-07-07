namespace Datateal.Core.Users;

/// <summary>
/// An interactive application user whose identity is delegated to an external identity provider
/// (e.g., Entra ID). A <see cref="UserAccount"/> can log in interactively; a
/// <see cref="ServiceAccount"/> cannot.
/// </summary>
public class UserAccount : AppUser
{
    /// <summary>
    /// External identity provider object ID (e.g., Entra ID OID).
    /// Populated on first successful login for stable future lookups.
    /// </summary>
    public string? ExternalId { get; set; }
}
