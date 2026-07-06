using Datateal.Core.Catalogs;
using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Catalogs;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Catalogs;
using Microsoft.Extensions.Options;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record GetManagedCatalogDuckLakeSettingsRequest(Guid CatalogId) : IRequest<ManagedCatalogDuckLakeSettingsDto?>;

internal class GetManagedCatalogDuckLakeSettingsHandler(
    ICatalogRepository repository,
    ICatalogDatabaseService databaseService,
    IOptions<CatalogSettings> settings)
    : IRequestHandler<GetManagedCatalogDuckLakeSettingsRequest, ManagedCatalogDuckLakeSettingsDto?>
{
    public async Task<ManagedCatalogDuckLakeSettingsDto?> Handle(
        GetManagedCatalogDuckLakeSettingsRequest request, CancellationToken cancellationToken)
    {
        var catalog = await repository.GetByIdAsync(request.CatalogId, cancellationToken);
        if (catalog is not ManagedCatalog) return null;

        var opts = settings.Value;
        var result = await databaseService.GetDuckLakeSettingsAsync(
            opts.CatalogHost, opts.CatalogPort, catalog.Name, opts.CatalogUser, opts.CatalogPassword,
            cancellationToken);
        return result is null ? null : new ManagedCatalogDuckLakeSettingsDto(result.Value.ParquetV2, result.Value.PerThreadOutput);
    }
}
