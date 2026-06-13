using Datateal.Data.Catalogs;
using Datateal.Orchestrator.Core.Interfaces;

namespace Datateal.Orchestrator.Infrastructure.Catalogs;

/// <inheritdoc cref="ICatalogAccessAuthorizer" />
internal class CatalogAccessAuthorizer(ICatalogAccessResolver resolver) : ICatalogAccessAuthorizer
{
    public async Task<IReadOnlyList<string>> GetInaccessibleAsync(
        Guid ownerUserId, Guid workspaceId, IReadOnlyList<string> catalogNames, CancellationToken ct = default)
    {
        if (catalogNames.Count == 0)
            return [];

        var accessible = await resolver.FilterAccessibleNamesAsync(ownerUserId, workspaceId, catalogNames, ct);
        var accessibleSet = accessible.ToHashSet(StringComparer.Ordinal);
        return catalogNames.Where(n => !accessibleSet.Contains(n)).ToList();
    }
}
