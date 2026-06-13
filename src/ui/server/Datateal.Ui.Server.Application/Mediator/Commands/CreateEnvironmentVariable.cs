using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Environment;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record CreateEnvironmentVariableRequest(Guid WorkspaceId, string Key, string Value, string? Description)
    : IRequest<EnvironmentVariableDto>;

internal class CreateEnvironmentVariableHandler(IEnvironmentRepository repository)
    : IRequestHandler<CreateEnvironmentVariableRequest, EnvironmentVariableDto>
{
    public async Task<EnvironmentVariableDto> Handle(CreateEnvironmentVariableRequest request, CancellationToken cancellationToken)
    {
        var variable = await repository.CreateVariableAsync(request.WorkspaceId, request.Key, request.Value, request.Description, cancellationToken);
        return new EnvironmentVariableDto(variable.Id, variable.Key, variable.Value, variable.Description, variable.CreatedAt, variable.UpdatedAt);
    }
}
