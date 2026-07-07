namespace Datateal.Core.Users;

/// <summary>
/// A non-interactive service identity created by an administrator. Service accounts cannot log
/// in via the identity provider. They are used as stable acting identities for API tokens
/// and automation pipelines, decoupling job ownership from any individual user.
/// </summary>
public class ServiceAccount : AppUser
{
    /// <summary>Optional description documenting the account's purpose.</summary>
    public string? Description { get; set; }
}
