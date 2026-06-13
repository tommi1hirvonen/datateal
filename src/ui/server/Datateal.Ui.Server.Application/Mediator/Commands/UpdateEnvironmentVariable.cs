using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Environment;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record UpdateEnvironmentVariableRequest(Guid WorkspaceId, Guid Id, string Key, string Value, string? Description)
    : IRequest<EnvironmentVariableDto?>;

internal class UpdateEnvironmentVariableHandler(IEnvironmentRepository repository)
    : IRequestHandler<UpdateEnvironmentVariableRequest, EnvironmentVariableDto?>
{
    public async Task<EnvironmentVariableDto?> Handle(UpdateEnvironmentVariableRequest request, CancellationToken cancellationToken)
    {
        var variable = await repository.UpdateVariableAsync(request.WorkspaceId, request.Id, request.Key, request.Value, request.Description, cancellationToken);
        if (variable is null) return null;
        return new EnvironmentVariableDto(variable.Id, variable.Key, variable.Value, variable.Description, variable.CreatedAt, variable.UpdatedAt);
    }
}
