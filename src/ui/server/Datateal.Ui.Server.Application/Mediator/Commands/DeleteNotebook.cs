using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record DeleteNotebookRequest(Guid WorkspaceId, Guid Id) : IRequest<bool>;

internal class DeleteNotebookHandler(IWorkspaceRepository repository) : IRequestHandler<DeleteNotebookRequest, bool>
{
    public Task<bool> Handle(DeleteNotebookRequest request, CancellationToken cancellationToken) =>
        repository.DeleteNotebookAsync(request.WorkspaceId, request.Id, cancellationToken);
}
