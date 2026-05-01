namespace DuckHouse.Ui.Shared.Users;

/// <summary>
/// Available role names for the UI role picker.
/// Mirrors DuckHouseRole constants from DuckHouse.Auth (which can't be referenced by WASM).
/// </summary>
public static class AvailableRoles
{
    public const string Admin = nameof(Admin);
    public const string NodePoolContributor = nameof(NodePoolContributor);
    public const string NodePoolOperator = nameof(NodePoolOperator);
    public const string JobContributor = nameof(JobContributor);
    public const string JobOperator = nameof(JobOperator);
    public const string JobReader = nameof(JobReader);
    public const string WorkspaceContributor = nameof(WorkspaceContributor);
    public const string CatalogContributor = nameof(CatalogContributor);
    public const string EnvironmentManager = nameof(EnvironmentManager);

    public static readonly string[] All =
    [
        Admin, NodePoolContributor, NodePoolOperator, JobContributor,
        JobOperator, JobReader, WorkspaceContributor, CatalogContributor,
        EnvironmentManager
    ];
}

public record AppUserDto(
    Guid Id,
    string Email,
    string? ExternalId,
    string DisplayName,
    bool IsEnabled,
    bool HasAllCatalogAccess,
    List<string> Roles,
    List<UserCatalogAccessDto> CatalogAccessList,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record UserCatalogAccessDto(
    Guid Id,
    Guid CatalogId,
    string CatalogName);

public record CreateUserRequest(
    string Email,
    string DisplayName,
    List<string> Roles,
    bool HasAllCatalogAccess);

public record UpdateUserRequest(
    string DisplayName,
    bool IsEnabled,
    List<string> Roles,
    bool HasAllCatalogAccess,
    List<Guid> CatalogIds);
