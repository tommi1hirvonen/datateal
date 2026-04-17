using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Catalogs;

namespace DuckHouse.Ui.Server.Application.Mediator.Queries;

public record GetCatalogsRequest : IRequest<IReadOnlyList<CatalogDto>>;

internal class GetCatalogsHandler(ICatalogRepository repository)
    : IRequestHandler<GetCatalogsRequest, IReadOnlyList<CatalogDto>>
{
    public async Task<IReadOnlyList<CatalogDto>> Handle(GetCatalogsRequest request, CancellationToken cancellationToken)
    {
        var catalogs = await repository.GetAllAsync(cancellationToken);
        return catalogs.Select(Commands.CreateCatalogHandler.ToDto).ToList();
    }
}
