using Datateal.Core.Users;
using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Users;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record UpdateUserCommand(
    Guid Id, string DisplayName, bool IsEnabled,
    List<string> Roles, bool HasAllCatalogAccess, List<Guid> CatalogIds)
    : IRequest<AppUserDto?>;

internal class UpdateUserHandler(IUserRepository repository) : IRequestHandler<UpdateUserCommand, AppUserDto?>
{
    public async Task<AppUserDto?> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var updated = await repository.UpdateAsync(
            request.Id, request.DisplayName, request.IsEnabled,
            request.Roles, request.HasAllCatalogAccess, request.CatalogIds,
            cancellationToken);
        return updated is UserAccount ua ? UserDtoMapper.ToDto(ua) : null;
    }
}
