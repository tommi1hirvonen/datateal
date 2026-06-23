using Datateal.Core.Mediator;
using Datateal.Core.RuntimePackages;
using Datateal.Ui.Server.Core.Repositories;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record GetWheelPackagesRequest(Guid WorkspaceId) : IRequest<IReadOnlyList<WheelPackage>>;

internal class GetWheelPackagesHandler(IWheelPackageRepository repository)
    : IRequestHandler<GetWheelPackagesRequest, IReadOnlyList<WheelPackage>>
{
    public Task<IReadOnlyList<WheelPackage>> Handle(GetWheelPackagesRequest request, CancellationToken cancellationToken) =>
        repository.GetAllAsync(request.WorkspaceId, cancellationToken);
}
