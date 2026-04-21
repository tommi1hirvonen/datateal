using DuckHouse.Core.Catalogs;
using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Catalogs;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Catalogs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace DuckHouse.Ui.Server.Application.Mediator.Queries;

public record GetCatalogMetadataRequest(Guid CatalogId) : IRequest<CatalogMetadataDto?>;

internal class GetCatalogMetadataHandler(
    ICatalogRepository repository,
    ICatalogMetadataService metadataService,
    IDataProtectionProvider dataProtection,
    IOptions<CatalogSettings> settings)
    : IRequestHandler<GetCatalogMetadataRequest, CatalogMetadataDto?>
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("DuckHouse.Catalogs");

    public async Task<CatalogMetadataDto?> Handle(GetCatalogMetadataRequest request, CancellationToken cancellationToken)
    {
        var catalog = await repository.GetByIdAsync(request.CatalogId, cancellationToken);
        if (catalog is null) return null;

        var opts = settings.Value;

        string catalogHost, catalogDatabase, catalogUser, catalogPassword;
        int catalogPort;

        if (catalog is ManagedCatalog)
        {
            catalogHost = opts.CatalogHost;
            catalogPort = opts.CatalogPort;
            catalogDatabase = catalog.Name;
            catalogUser = opts.CatalogUser;
            catalogPassword = opts.CatalogPassword;
        }
        else
        {
            var u = (UnmanagedCatalog)catalog;
            catalogHost = u.CatalogHost;
            catalogPort = u.CatalogPort;
            catalogDatabase = u.CatalogDatabase;
            catalogUser = u.CatalogUser;
            catalogPassword = u.EncryptedCatalogPassword is not null
                ? _protector.Unprotect(u.EncryptedCatalogPassword)
                : string.Empty;
        }

        var result = await metadataService.GetMetadataAsync(
            catalogHost,
            catalogPort,
            catalogDatabase,
            catalogUser,
            catalogPassword,
            cancellationToken);

        return new CatalogMetadataDto(
            result.Schemas.Select(s => new SchemaDto(
                s.Name,
                s.Tables.Select(t => new TableDto(
                    t.Name, t.Type,
                    t.Columns.Select(c => new ColumnDto(c.Name, c.DataType, c.IsNullable, c.OrdinalPosition, c.Comment)).ToList(),
                    t.Comment
                )).ToList()
            )).ToList());
    }
}
