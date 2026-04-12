using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record DeleteWheelPackageRequest(Guid Id) : IRequest<bool>;

internal class DeleteWheelPackageHandler(IWheelPackageRepository repository)
    : IRequestHandler<DeleteWheelPackageRequest, bool>
{
    public Task<bool> Handle(DeleteWheelPackageRequest request, CancellationToken cancellationToken) =>
        repository.DeleteAsync(request.Id, cancellationToken);
}
