using DuckHouse.Core.Mediator;
using DuckHouse.Core.Users;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Users;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

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
        return updated is not null ? UserDtoMapper.ToDto(updated) : null;
    }
}
