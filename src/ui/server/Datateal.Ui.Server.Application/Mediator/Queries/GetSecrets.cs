using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Environment;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record GetSecretsRequest(Guid WorkspaceId) : IRequest<IReadOnlyList<SecretDto>>;

internal class GetSecretsHandler(IEnvironmentRepository repository)
    : IRequestHandler<GetSecretsRequest, IReadOnlyList<SecretDto>>
{
    public async Task<IReadOnlyList<SecretDto>> Handle(GetSecretsRequest request, CancellationToken cancellationToken)
    {
        var secrets = await repository.GetSecretsAsync(request.WorkspaceId, cancellationToken);
        return secrets.Select(s => new SecretDto(
            s.Id, s.Key, s.Description, s.CreatedAt, s.UpdatedAt)).ToList();
    }
}
