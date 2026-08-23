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

    private static List<string> NormalizeCatalogs(List<string>? catalogs) =>
        (catalogs ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
