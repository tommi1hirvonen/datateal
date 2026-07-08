using Datateal.Core.Users;
using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Users;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record GetUserRequest(Guid Id) : IRequest<AppUserDto?>;

internal class GetUserHandler(IUserRepository repository) : IRequestHandler<GetUserRequest, AppUserDto?>
{
    public async Task<AppUserDto?> Handle(GetUserRequest request, CancellationToken cancellationToken)
    {
        var user = await repository.GetByIdAsync(request.Id, cancellationToken);
        return user is UserAccount ua ? Commands.UserDtoMapper.ToDto(ua) : null;
    }
}
