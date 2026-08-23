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
        IEnumerable<string> existingCatalogNames,
        IReadOnlyDictionary<string, string>? variables = null,
        IReadOnlyDictionary<string, string>? env = null)
    {
        var errors = new List<string>();

        var existingCatalogSet = new HashSet<string>(existingCatalogNames, StringComparer.OrdinalIgnoreCase);

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

                // Password is required when creating a new catalog; optional on updates (preserves existing).
                var isNew = !string.IsNullOrWhiteSpace(catalog.Name) && !existingCatalogSet.Contains(catalog.Name);
                if (isNew && string.IsNullOrWhiteSpace(catalog.CatalogPassword))
                    errors.Add($"Unmanaged catalog '{catalog.Name}' is new and requires 'catalog_password'. " +
                               "Use ${env.VAR_NAME} to inject it from your CI/CD pipeline.");
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

        // ── Variable/env substitution checks ───────────────────────────────────
        // Resolved here (purely for validation — results are discarded) so an unresolved
        // ${var.X}/${env.X} token is reported during Plan, not only when Apply actually
        // substitutes the value.

        foreach (var catalog in bundle.Catalogs)
        {
            ValidateSubstitutable(catalog.DataPath, variables, env, errors);
            ValidateSubstitutable(catalog.CatalogHost, variables, env, errors);
            ValidateSubstitutable(catalog.CatalogDatabase, variables, env, errors);
            ValidateSubstitutable(catalog.CatalogUser, variables, env, errors);
            ValidateSubstitutable(catalog.CatalogPassword, variables, env, errors);
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Bundle validation failed with {errors.Count} error(s):\n" +
                string.Join("\n", errors.Select((e, i) => $"  {i + 1}. {e}")));
    }

    /// <summary>
    /// Attempts to resolve every <c>${var.X}</c>/<c>${env.X}</c> token in <paramref name="value"/>,
    /// discarding the result — only used to surface <see cref="DeploymentVariableException"/>
    /// messages as ordinary validation errors instead of letting them propagate individually.
    /// </summary>
    private static void ValidateSubstitutable(
        string? value,
        IReadOnlyDictionary<string, string>? variables,
        IReadOnlyDictionary<string, string>? env,
        List<string> errors)
    {
        if (string.IsNullOrEmpty(value)) return;

        try
        {
            VariableSubstitution.Substitute(value, variables, env);
        }
        catch (DeploymentVariableException ex)
        {
            errors.Add(ex.Message);
        }
    }

    private static HashSet<string> BuildSet(IEnumerable<string> bundleValues, IEnumerable<string> existingValues) =>
        new(bundleValues.Concat(existingValues).Where(v => !string.IsNullOrWhiteSpace(v)),
            StringComparer.OrdinalIgnoreCase);
}
