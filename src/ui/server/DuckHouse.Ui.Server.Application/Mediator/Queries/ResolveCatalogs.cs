using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Catalogs;
using Microsoft.AspNetCore.DataProtection;

namespace DuckHouse.Ui.Server.Application.Mediator.Queries;

public record ResolveCatalogsRequest(IReadOnlyList<string> CatalogNames) : IRequest<IReadOnlyList<ResolvedCatalogDto>>;

internal class ResolveCatalogsHandler(ICatalogRepository repository, IDataProtectionProvider dataProtection)
    : IRequestHandler<ResolveCatalogsRequest, IReadOnlyList<ResolvedCatalogDto>>
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("DuckHouse.Catalogs");

    public async Task<IReadOnlyList<ResolvedCatalogDto>> Handle(ResolveCatalogsRequest request, CancellationToken cancellationToken)
    {
        if (request.CatalogNames.Count == 0) return [];

        var catalogs = await repository.GetByNamesAsync(request.CatalogNames, cancellationToken);

        return catalogs.Select(c => new ResolvedCatalogDto(
            c.Name,
            c.DataPath ?? string.Empty,
            c.EncryptedStorageConnectionString is not null
                ? _protector.Unprotect(c.EncryptedStorageConnectionString)
                : null,
            c.CatalogHost ?? string.Empty,
            c.CatalogPort ?? 5432,
            c.CatalogDatabase ?? c.Name,
            c.CatalogUser ?? string.Empty,
            c.EncryptedCatalogPassword is not null
                ? _protector.Unprotect(c.EncryptedCatalogPassword)
                : string.Empty
        )).ToList();
    }
}
