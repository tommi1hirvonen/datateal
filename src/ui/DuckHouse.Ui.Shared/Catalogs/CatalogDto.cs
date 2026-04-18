using System.Text.Json.Serialization;

namespace DuckHouse.Ui.Shared.Catalogs;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ManagedCatalogDto), "Managed")]
[JsonDerivedType(typeof(UnmanagedCatalogDto), "Unmanaged")]
public abstract record CatalogDto(
    Guid Id,
    string Name,
    string DataPath,
    string CatalogHost,
    int CatalogPort,
    string CatalogDatabase,
    string CatalogUser,
    bool HasStorageConnectionString,
    bool HasCatalogPassword,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Connection details are read from server-side CatalogSettings at runtime.
/// </summary>
public sealed record ManagedCatalogDto(
    Guid Id,
    string Name,
    string DataPath,
    string CatalogHost,
    int CatalogPort,
    string CatalogDatabase,
    string CatalogUser,
    bool HasStorageConnectionString,
    bool HasCatalogPassword,
    DateTime CreatedAt,
    DateTime UpdatedAt)
    : CatalogDto(Id, Name, DataPath, CatalogHost, CatalogPort,
        CatalogDatabase, CatalogUser, HasStorageConnectionString, HasCatalogPassword,
        CreatedAt, UpdatedAt);

/// <summary>
/// Connection details are stored in the database (user-supplied).
/// </summary>
public sealed record UnmanagedCatalogDto(
    Guid Id,
    string Name,
    string DataPath,
    string CatalogHost,
    int CatalogPort,
    string CatalogDatabase,
    string CatalogUser,
    bool HasStorageConnectionString,
    bool HasCatalogPassword,
    DateTime CreatedAt,
    DateTime UpdatedAt)
    : CatalogDto(Id, Name, DataPath, CatalogHost, CatalogPort,
        CatalogDatabase, CatalogUser, HasStorageConnectionString, HasCatalogPassword,
        CreatedAt, UpdatedAt);
