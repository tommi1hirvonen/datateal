using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Core.RuntimePackages;

namespace DuckHouse.Ui.Server.Application.Mediator.Queries;

public record GetWheelPackagesRequest : IRequest<IReadOnlyList<WheelPackage>>;

internal class GetWheelPackagesHandler(IWheelPackageRepository repository)
    : IRequestHandler<GetWheelPackagesRequest, IReadOnlyList<WheelPackage>>
{
    public Task<IReadOnlyList<WheelPackage>> Handle(GetWheelPackagesRequest request, CancellationToken cancellationToken) =>
        repository.GetAllAsync(cancellationToken);
}
