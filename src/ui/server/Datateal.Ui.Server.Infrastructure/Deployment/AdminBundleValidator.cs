using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

/// <summary>
/// Validates all cross-references and required fields within an admin-scope bundle
/// against the bundle itself and the existing tenant state. Collects every error before throwing.
/// </summary>
internal static class AdminBundleValidator
{
    private static readonly HashSet<string> ValidCatalogTypes =
        new(["managed", "unmanaged"], StringComparer.OrdinalIgnoreCase);

    public static void Validate(
        Bundle bundle,
        IEnumerable<string> existingWorkspaceNames,
        IEnumerable<string> existingCatalogNames)
    {
        var errors = new List<string>();

        // ── Required fields ────────────────────────────────────────────────────

        foreach (var workspace in bundle.Workspaces)
            if (string.IsNullOrWhiteSpace(workspace.Name))
                errors.Add("A workspace entry is missing a required 'name'.");

        foreach (var catalog in bundle.Catalogs)
        {
            if (string.IsNullOrWhiteSpace(catalog.Name))
                errors.Add("A catalog entry is missing a required 'name'.");

            var catalogType = string.IsNullOrWhiteSpace(catalog.Type) ? "managed" : catalog.Type.Trim();
            if (!ValidCatalogTypes.Contains(catalogType))
                errors.Add($"Catalog '{catalog.Name}' has unknown type '{catalog.Type}'. Expected 'managed' or 'unmanaged'.");

            if (string.Equals(catalogType, "managed", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(catalog.DataPath))
                    errors.Add($"Managed catalog '{catalog.Name}' is missing required field 'data_path'.");
            }
            else if (string.Equals(catalogType, "unmanaged", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(catalog.CatalogHost))
                    errors.Add($"Unmanaged catalog '{catalog.Name}' is missing required field 'catalog_host'.");
                if (string.IsNullOrWhiteSpace(catalog.CatalogDatabase))
                    errors.Add($"Unmanaged catalog '{catalog.Name}' is missing required field 'catalog_database'.");
                if (string.IsNullOrWhiteSpace(catalog.CatalogUser))
                    errors.Add($"Unmanaged catalog '{catalog.Name}' is missing required field 'catalog_user'.");
            }
        }

        foreach (var membership in bundle.Memberships)
            if (string.IsNullOrWhiteSpace(membership.Workspace))
                errors.Add("A membership entry is missing a required 'workspace'.");

        foreach (var access in bundle.UserCatalogAccess)
            if (string.IsNullOrWhiteSpace(access.Email))
                errors.Add("A user catalog access entry is missing a required 'email'.");

        // ── Cross-reference checks ─────────────────────────────────────────────

        var availableWorkspaces = BuildSet(
            bundle.Workspaces.Select(w => w.Name),
            existingWorkspaceNames);

        var availableCatalogs = BuildSet(
            bundle.Catalogs.Select(c => c.Name),
            existingCatalogNames);

        foreach (var catalog in bundle.Catalogs)
            foreach (var wsName in catalog.WorkspaceAccess ?? [])
                if (!availableWorkspaces.Contains(wsName))
                    errors.Add($"Catalog '{catalog.Name}' grants access to workspace '{wsName}' which does not exist in the tenant or bundle.");

        foreach (var membership in bundle.Memberships)
            if (!string.IsNullOrWhiteSpace(membership.Workspace) && !availableWorkspaces.Contains(membership.Workspace))
                errors.Add($"Membership entry references workspace '{membership.Workspace}' which does not exist in the tenant or bundle.");

        foreach (var access in bundle.UserCatalogAccess)
        {
            if (access.HasAllCatalogAccess) continue;
            foreach (var catalogName in access.AllowedCatalogs ?? [])
                if (!availableCatalogs.Contains(catalogName))
                    errors.Add($"User '{access.Email}' catalog access references catalog '{catalogName}' which does not exist in the tenant or bundle.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Bundle validation failed with {errors.Count} error(s):\n" +
                string.Join("\n", errors.Select((e, i) => $"  {i + 1}. {e}")));
    }

    private static HashSet<string> BuildSet(IEnumerable<string> bundleValues, IEnumerable<string> existingValues) =>
        new(bundleValues.Concat(existingValues).Where(v => !string.IsNullOrWhiteSpace(v)),
            StringComparer.OrdinalIgnoreCase);
}
