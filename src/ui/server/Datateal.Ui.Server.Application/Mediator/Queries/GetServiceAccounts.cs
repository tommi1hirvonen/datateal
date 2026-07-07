using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Users;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record GetServiceAccountsRequest : IRequest<IReadOnlyList<ServiceAccountDto>>;

internal class GetServiceAccountsHandler(IUserRepository repository)
    : IRequestHandler<GetServiceAccountsRequest, IReadOnlyList<ServiceAccountDto>>
{
    public async Task<IReadOnlyList<ServiceAccountDto>> Handle(
        GetServiceAccountsRequest request, CancellationToken cancellationToken)
    {
        var accounts = await repository.GetAllServiceAccountsAsync(cancellationToken);
        return accounts.Select(Commands.UserDtoMapper.ToServiceAccountDto).ToList();
    }
}
