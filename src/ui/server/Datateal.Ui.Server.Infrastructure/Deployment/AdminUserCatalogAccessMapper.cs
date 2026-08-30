using Datateal.Core.Users;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

internal sealed class AdminUserCatalogAccessMapper : IResourceMapper<UserCatalogAccessModel>
{
    public string ResourceType => "user_catalog_access";

    public UserCatalogAccessModel ToModel(AppUser user) => new()
    {
        Email = user.Email,
        HasAllCatalogAccess = user.HasAllCatalogAccess,
        AllowedCatalogs = user.CatalogAccessList
            .Select(access => access.Catalog.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList(),
    };

    public string NaturalKey(UserCatalogAccessModel model) => model.Email.Trim();

    public bool AreEqual(UserCatalogAccessModel desired, UserCatalogAccessModel current)
    {
        if (!string.Equals(NaturalKey(desired), NaturalKey(current), StringComparison.OrdinalIgnoreCase))
            return false;

        if (desired.HasAllCatalogAccess != current.HasAllCatalogAccess)
            return false;

        if (desired.HasAllCatalogAccess)
            return true;

        var currentCatalogs = NormalizeCatalogs(current.AllowedCatalogs);
        return NormalizeCatalogs(desired.AllowedCatalogs).SequenceEqual(currentCatalogs, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Surfaces the <c>has_all_catalog_access</c> toggle and exactly which individual catalogs
    /// were granted or revoked — an "Update" on the user as a whole would otherwise hide which
    /// specific catalog access changed.
    /// </summary>
    public List<FieldChange>? DiffDetails(UserCatalogAccessModel desired, UserCatalogAccessModel current)
    {
        if (desired.HasAllCatalogAccess != current.HasAllCatalogAccess)
        {
            return
            [
                new FieldChange
                {
                    Field = "has_all_catalog_access",
                    Before = current.HasAllCatalogAccess.ToString(),
                    After = desired.HasAllCatalogAccess.ToString(),
                },
            ];
        }

        var desiredCatalogs = NormalizeCatalogs(desired.AllowedCatalogs).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentCatalogs = NormalizeCatalogs(current.AllowedCatalogs).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var details = new List<FieldChange>();

        foreach (var catalog in desiredCatalogs.Except(currentCatalogs, StringComparer.OrdinalIgnoreCase).OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            details.Add(new FieldChange { Field = catalog, Before = "(none)", After = "granted" });

        foreach (var catalog in currentCatalogs.Except(desiredCatalogs, StringComparer.OrdinalIgnoreCase).OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            details.Add(new FieldChange { Field = catalog, Before = "granted", After = "(none)" });

        return details.Count > 0 ? details : null;
    }

    private static List<string> NormalizeCatalogs(List<string>? catalogs) =>
        (catalogs ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
