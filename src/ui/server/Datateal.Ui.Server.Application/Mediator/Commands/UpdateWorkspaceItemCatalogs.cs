using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record UpdateWorkspaceItemCatalogsRequest(Guid WorkspaceId, Guid ItemId, List<string> CatalogNames) : IRequest<bool>;

internal class UpdateWorkspaceItemCatalogsHandler(IWorkspaceRepository repository, ICatalogRepository catalogRepository)
    : IRequestHandler<UpdateWorkspaceItemCatalogsRequest, bool>
{
    public async Task<bool> Handle(UpdateWorkspaceItemCatalogsRequest request, CancellationToken cancellationToken)
    {
        var item = await repository.GetItemAsync(request.WorkspaceId, request.ItemId, cancellationToken);
        if (item is null) return false;

        // Validate that all referenced catalogs exist and are accessible from this item's workspace.
        if (request.CatalogNames.Count > 0)
        {
            var accessible = await catalogRepository.GetWorkspaceAccessibleNamesAsync(
                item.WorkspaceId, request.CatalogNames, cancellationToken);
            var accessibleSet = accessible.ToHashSet();
            var blocked = request.CatalogNames.Where(n => !accessibleSet.Contains(n)).ToList();
            if (blocked.Count > 0)
                throw new InvalidOperationException(
                    $"Catalogs not accessible from this workspace: {string.Join(", ", blocked)}");
        }

        item.CatalogNames = request.CatalogNames.Count > 0 ? request.CatalogNames : null;
        item.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateItemCatalogNamesAsync(request.WorkspaceId, request.ItemId, item.CatalogNames, cancellationToken);
        return true;
    }
}
