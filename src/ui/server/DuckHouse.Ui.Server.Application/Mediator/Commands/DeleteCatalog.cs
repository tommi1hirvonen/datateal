using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Catalogs;
using DuckHouse.Ui.Server.Core.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record DeleteCatalogRequest(Guid Id) : IRequest<bool>;

internal class DeleteCatalogHandler(
    ICatalogRepository repository,
    ICatalogDatabaseService databaseService,
    IDataProtectionProvider dataProtection,
    IOptions<CatalogSettings> settings)
    : IRequestHandler<DeleteCatalogRequest, bool>
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("DuckHouse.Catalogs");

    public async Task<bool> Handle(DeleteCatalogRequest request, CancellationToken cancellationToken)
    {
        var catalog = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (catalog is null) return false;

        // Drop the PostgreSQL database for managed catalogs
        if (catalog.IsManaged)
        {
            var opts = settings.Value;
            var password = !string.IsNullOrEmpty(catalog.EncryptedCatalogPassword)
                ? _protector.Unprotect(catalog.EncryptedCatalogPassword)
                : opts.CatalogPassword;

            await databaseService.DropDatabaseAsync(
                catalog.CatalogDatabase ?? catalog.Name,
                catalog.CatalogHost ?? opts.CatalogHost,
                catalog.CatalogPort ?? opts.CatalogPort,
                catalog.CatalogUser ?? opts.CatalogUser,
                password,
                cancellationToken);
        }

        return await repository.DeleteAsync(request.Id, cancellationToken);
    }
}
