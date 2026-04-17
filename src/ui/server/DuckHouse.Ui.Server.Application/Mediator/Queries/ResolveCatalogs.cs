using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Catalogs;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Catalogs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace DuckHouse.Ui.Server.Application.Mediator.Queries;

public record ResolveCatalogsRequest(IReadOnlyList<string> CatalogNames) : IRequest<IReadOnlyList<ResolvedCatalogDto>>;

internal class ResolveCatalogsHandler(
    ICatalogRepository repository,
    IDataProtectionProvider dataProtection,
    IOptions<CatalogSettings> settings)
    : IRequestHandler<ResolveCatalogsRequest, IReadOnlyList<ResolvedCatalogDto>>
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("DuckHouse.Catalogs");

    public async Task<IReadOnlyList<ResolvedCatalogDto>> Handle(ResolveCatalogsRequest request, CancellationToken cancellationToken)
    {
        if (request.CatalogNames.Count == 0) return [];

        var catalogs = await repository.GetByNamesAsync(request.CatalogNames, cancellationToken);
        var opts = settings.Value;

        return catalogs.Select(c =>
        {
            if (c.IsManaged)
            {
                return new ResolvedCatalogDto(
                    c.Name,
                    DataPath: opts.BaseDataPath.TrimEnd('/') + "/" + c.Name,
                    StorageConnectionString: !string.IsNullOrEmpty(opts.StorageConnectionString)
                        ? opts.StorageConnectionString
                        : null,
                    CatalogHost: opts.CatalogHost,
                    CatalogPort: opts.CatalogPort,
                    CatalogDatabase: c.Name,
                    CatalogUser: opts.CatalogUser,
                    CatalogPassword: opts.CatalogPassword);
            }

            return new ResolvedCatalogDto(
                c.Name,
                DataPath: c.DataPath ?? string.Empty,
                StorageConnectionString: c.EncryptedStorageConnectionString is not null
                    ? _protector.Unprotect(c.EncryptedStorageConnectionString)
                    : null,
                CatalogHost: c.CatalogHost ?? string.Empty,
                CatalogPort: c.CatalogPort ?? 5432,
                CatalogDatabase: c.CatalogDatabase ?? c.Name,
                CatalogUser: c.CatalogUser ?? string.Empty,
                CatalogPassword: c.EncryptedCatalogPassword is not null
                    ? _protector.Unprotect(c.EncryptedCatalogPassword)
                    : string.Empty);
        }).ToList();
    }
}
