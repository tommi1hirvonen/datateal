using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Users;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record GetUsersRequest : IRequest<IReadOnlyList<AppUserDto>>;

internal class GetUsersHandler(IUserRepository repository) : IRequestHandler<GetUsersRequest, IReadOnlyList<AppUserDto>>
{
    public async Task<IReadOnlyList<AppUserDto>> Handle(GetUsersRequest request, CancellationToken cancellationToken)
    {
        var users = await repository.GetAllUserAccountsAsync(cancellationToken);
        return users.Select(Commands.UserDtoMapper.ToDto).ToList();
    }
}
