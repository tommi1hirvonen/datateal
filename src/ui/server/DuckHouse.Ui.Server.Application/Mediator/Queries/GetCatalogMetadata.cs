using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Catalogs;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Catalogs;
using Microsoft.AspNetCore.DataProtection;

namespace DuckHouse.Ui.Server.Application.Mediator.Queries;

public record GetCatalogMetadataRequest(Guid CatalogId) : IRequest<CatalogMetadataDto?>;

internal class GetCatalogMetadataHandler(
    ICatalogRepository repository,
    ICatalogMetadataService metadataService,
    IDataProtectionProvider dataProtection)
    : IRequestHandler<GetCatalogMetadataRequest, CatalogMetadataDto?>
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("DuckHouse.Catalogs");

    public async Task<CatalogMetadataDto?> Handle(GetCatalogMetadataRequest request, CancellationToken cancellationToken)
    {
        var catalog = await repository.GetByIdAsync(request.CatalogId, cancellationToken);
        if (catalog is null) return null;

        var password = catalog.EncryptedCatalogPassword is not null
            ? _protector.Unprotect(catalog.EncryptedCatalogPassword)
            : string.Empty;

        var storageConnectionString = catalog.EncryptedStorageConnectionString is not null
            ? _protector.Unprotect(catalog.EncryptedStorageConnectionString)
            : null;

        var result = await metadataService.GetMetadataAsync(
            catalog.Name,
            catalog.DataPath ?? string.Empty,
            storageConnectionString,
            catalog.CatalogHost ?? string.Empty,
            catalog.CatalogPort ?? 5432,
            catalog.CatalogDatabase ?? catalog.Name,
            catalog.CatalogUser ?? string.Empty,
            password,
            cancellationToken);

        return new CatalogMetadataDto(
            result.Schemas.Select(s => new SchemaDto(
                s.Name,
                s.Tables.Select(t => new TableDto(
                    t.Name, t.Type,
                    t.Columns.Select(c => new ColumnDto(c.Name, c.DataType, c.IsNullable, c.OrdinalPosition)).ToList()
                )).ToList()
            )).ToList());
    }
}
