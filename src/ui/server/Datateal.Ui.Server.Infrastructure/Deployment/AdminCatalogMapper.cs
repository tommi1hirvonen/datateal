using Datateal.Core.Catalogs;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

internal sealed class AdminCatalogMapper : IResourceMapper<CatalogModel>
{
    public string ResourceType => "catalog";

    public CatalogModel ToModel(Catalog catalog, IEnumerable<string> workspaceAccessNames) => new()
    {
        Name = catalog.Name,
        Type = catalog is ManagedCatalog ? "managed" : "unmanaged",
        AccessibleFromAllWorkspaces = catalog.AccessibleFromAllWorkspaces,
        WorkspaceAccess = workspaceAccessNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList(),
        DataPath = catalog is UnmanagedCatalog unmanaged ? unmanaged.DataPath : null,
        CatalogHost = catalog is UnmanagedCatalog unmanaged2 ? unmanaged2.CatalogHost : null,
        CatalogDatabase = catalog is UnmanagedCatalog unmanaged3 ? unmanaged3.CatalogDatabase : null,
        CatalogUser = catalog is UnmanagedCatalog unmanaged4 ? unmanaged4.CatalogUser : null,
    };

    public string NaturalKey(CatalogModel model) => model.Name.Trim();

    public bool AreEqual(CatalogModel desired, CatalogModel current)
    {
        if (!string.Equals(NaturalKey(desired), NaturalKey(current), StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals((desired.Type ?? "managed").Trim(), (current.Type ?? "managed").Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        if ((desired.AccessibleFromAllWorkspaces ?? true) != (current.AccessibleFromAllWorkspaces ?? true))
            return false;

        if (!string.Equals(desired.DataPath, current.DataPath, StringComparison.Ordinal)
            || !string.Equals(desired.CatalogHost, current.CatalogHost, StringComparison.Ordinal)
            || !string.Equals(desired.CatalogDatabase, current.CatalogDatabase, StringComparison.Ordinal)
            || !string.Equals(desired.CatalogUser, current.CatalogUser, StringComparison.Ordinal))
            return false;

        if (desired.AccessibleFromAllWorkspaces == false)
        {
            return NormalizeList(desired.WorkspaceAccess)
                .SequenceEqual(NormalizeList(current.WorkspaceAccess), StringComparer.OrdinalIgnoreCase);
        }

        return true;
    }

    private static List<string> NormalizeList(List<string>? values) =>
        (values ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
