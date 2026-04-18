using DuckHouse.Core.Catalogs;
using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Catalogs;
using DuckHouse.Ui.Server.Core.Repositories;
using Microsoft.Extensions.Options;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record DeleteCatalogRequest(Guid Id) : IRequest<bool>;

internal class DeleteCatalogHandler(
    ICatalogRepository repository,
    ICatalogDatabaseService databaseService,
    IOptions<CatalogSettings> settings)
    : IRequestHandler<DeleteCatalogRequest, bool>
{
    public async Task<bool> Handle(DeleteCatalogRequest request, CancellationToken cancellationToken)
    {
        var catalog = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (catalog is null) return false;

        // Drop the PostgreSQL database for managed catalogs
        if (catalog is ManagedCatalog)
        {
            var opts = settings.Value;
            await databaseService.DropDatabaseAsync(
                catalog.Name,
                opts.CatalogHost,
                opts.CatalogPort,
                opts.CatalogUser,
                opts.CatalogPassword,
                cancellationToken);
        }

        return await repository.DeleteAsync(request.Id, cancellationToken);
    }
}
