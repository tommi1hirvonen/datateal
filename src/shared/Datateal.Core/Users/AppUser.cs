namespace Datateal.Core.Users;

/// <summary>
/// Abstract base for all application identities. Concrete subtypes are
/// <see cref="UserAccount"/> (interactive, IdP-bound) and <see cref="ServiceAccount"/>
/// (admin-created, non-interactive). The EF TPH discriminator column is <c>UserType varchar(32)</c>.
/// </summary>
public abstract class AppUser
{
    public Guid Id { get; set; }

    /// <summary>
    /// Primary identifier for admin management (email/UPN for user accounts;
    /// a descriptive label for service accounts).
    /// </summary>
    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// If true, the identity can access all catalogs (present and future).
    /// Admin and CatalogContributor roles have implicit access regardless of this flag.
    /// </summary>
    public bool HasAllCatalogAccess { get; set; }

    /// <summary>
    /// Application roles. Stored as a JSON array (EF Core primitive collection).
    /// </summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>
    /// Explicit catalog access grants (only relevant when <see cref="HasAllCatalogAccess"/> is false
    /// and the identity does not hold Admin or CatalogContributor roles).
    /// </summary>
    public List<UserCatalogAccess> CatalogAccessList { get; set; } = [];
}
