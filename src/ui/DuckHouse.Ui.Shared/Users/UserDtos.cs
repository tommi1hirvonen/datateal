namespace DuckHouse.Ui.Shared.Users;

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
    bool HasAllCatalogAccess,
    List<Guid> CatalogIds);

public record UpdateUserRequest(
    string DisplayName,
    bool IsEnabled,
    List<string> Roles,
    bool HasAllCatalogAccess,
    List<Guid> CatalogIds);
