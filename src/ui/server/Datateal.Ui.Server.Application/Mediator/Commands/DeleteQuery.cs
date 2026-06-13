using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record DeleteQueryRequest(Guid WorkspaceId, Guid Id) : IRequest<bool>;

internal class DeleteQueryHandler(IWorkspaceRepository repository) : IRequestHandler<DeleteQueryRequest, bool>
{
    public Task<bool> Handle(DeleteQueryRequest request, CancellationToken cancellationToken) =>
        repository.DeleteQueryAsync(request.WorkspaceId, request.Id, cancellationToken);
}
