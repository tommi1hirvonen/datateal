using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record UpdateWorkspaceItemCatalogsRequest(Guid ItemId, List<string> CatalogNames) : IRequest<bool>;

internal class UpdateWorkspaceItemCatalogsHandler(IWorkspaceRepository repository, ICatalogRepository catalogRepository)
    : IRequestHandler<UpdateWorkspaceItemCatalogsRequest, bool>
{
    public async Task<bool> Handle(UpdateWorkspaceItemCatalogsRequest request, CancellationToken cancellationToken)
    {
        var item = await repository.GetItemAsync(request.ItemId, cancellationToken);
        if (item is null) return false;

        // Validate that all catalog names exist
        if (request.CatalogNames.Count > 0)
        {
            var catalogs = await catalogRepository.GetByNamesAsync(request.CatalogNames, cancellationToken);
            var found = catalogs.Select(c => c.Name).ToHashSet();
            var missing = request.CatalogNames.Where(n => !found.Contains(n)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"Catalogs not found: {string.Join(", ", missing)}");
        }

        item.CatalogNames = request.CatalogNames.Count > 0 ? request.CatalogNames : null;
        item.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateItemCatalogNamesAsync(request.ItemId, item.CatalogNames, cancellationToken);
        return true;
    }
}
