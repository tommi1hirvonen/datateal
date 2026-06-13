using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record DeleteWheelPackageRequest(Guid WorkspaceId, Guid Id) : IRequest<bool>;

internal class DeleteWheelPackageHandler(IWheelPackageRepository repository)
    : IRequestHandler<DeleteWheelPackageRequest, bool>
{
    public Task<bool> Handle(DeleteWheelPackageRequest request, CancellationToken cancellationToken) =>
        repository.DeleteAsync(request.WorkspaceId, request.Id, cancellationToken);
}
