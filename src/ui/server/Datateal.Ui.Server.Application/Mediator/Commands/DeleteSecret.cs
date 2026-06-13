using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record DeleteSecretRequest(Guid WorkspaceId, Guid Id) : IRequest<bool>;

internal class DeleteSecretHandler(IEnvironmentRepository repository)
    : IRequestHandler<DeleteSecretRequest, bool>
{
    public Task<bool> Handle(DeleteSecretRequest request, CancellationToken cancellationToken) =>
        repository.DeleteSecretAsync(request.WorkspaceId, request.Id, cancellationToken);
}
