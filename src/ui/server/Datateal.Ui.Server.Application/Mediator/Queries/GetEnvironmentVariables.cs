using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Environment;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record GetEnvironmentVariablesRequest(Guid WorkspaceId) : IRequest<IReadOnlyList<EnvironmentVariableDto>>;

internal class GetEnvironmentVariablesHandler(IEnvironmentRepository repository)
    : IRequestHandler<GetEnvironmentVariablesRequest, IReadOnlyList<EnvironmentVariableDto>>
{
    public async Task<IReadOnlyList<EnvironmentVariableDto>> Handle(GetEnvironmentVariablesRequest request, CancellationToken cancellationToken)
    {
        var variables = await repository.GetVariablesAsync(request.WorkspaceId, cancellationToken);
        return variables.Select(v => new EnvironmentVariableDto(
            v.Id, v.Key, v.Value, v.Description, v.CreatedAt, v.UpdatedAt)).ToList();
    }
}
