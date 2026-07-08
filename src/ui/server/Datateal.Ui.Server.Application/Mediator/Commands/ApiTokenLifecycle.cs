using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record RevokeApiTokenCommand(Guid Id) : IRequest<bool>;

internal class RevokeApiTokenHandler(IApiTokenRepository repository)
    : IRequestHandler<RevokeApiTokenCommand, bool>
{
    public Task<bool> Handle(RevokeApiTokenCommand request, CancellationToken cancellationToken) =>
        repository.RevokeAsync(request.Id, cancellationToken);
}

public record DeleteApiTokenCommand(Guid Id) : IRequest<bool>;

internal class DeleteApiTokenHandler(IApiTokenRepository repository)
    : IRequestHandler<DeleteApiTokenCommand, bool>
{
    public Task<bool> Handle(DeleteApiTokenCommand request, CancellationToken cancellationToken) =>
        repository.DeleteAsync(request.Id, cancellationToken);
}
