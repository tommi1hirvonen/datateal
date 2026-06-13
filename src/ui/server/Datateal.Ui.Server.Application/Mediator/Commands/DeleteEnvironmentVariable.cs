using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record DeleteEnvironmentVariableRequest(Guid WorkspaceId, Guid Id) : IRequest<bool>;

internal class DeleteEnvironmentVariableHandler(IEnvironmentRepository repository)
    : IRequestHandler<DeleteEnvironmentVariableRequest, bool>
{
    public Task<bool> Handle(DeleteEnvironmentVariableRequest request, CancellationToken cancellationToken) =>
        repository.DeleteVariableAsync(request.WorkspaceId, request.Id, cancellationToken);
}
