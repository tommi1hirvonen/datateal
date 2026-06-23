using Datateal.Core.Mediator;
using Datateal.Core.Nodes;
using Datateal.Ui.Server.Core.Repositories;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record CreateNodeRequest(
    string Name,
    string VmSize,
    TimeSpan? KernelIdleTimeout,
    TimeSpan? NodeIdleTimeout,
    string? KernelRequirements,
    IReadOnlyList<WheelContent>? WheelContents,
    IReadOnlyDictionary<string, string>? EnvironmentVariables,
    IReadOnlyDictionary<string, string>? Secrets) : IRequest<NodeInfo>;

internal class CreateNodeHandler(INodeRepository nodeRepository) : IRequestHandler<CreateNodeRequest, NodeInfo>
{
    public Task<NodeInfo> Handle(CreateNodeRequest request, CancellationToken cancellationToken) =>
        nodeRepository.CreateNodeAsync(
            request.Name,
            request.VmSize,
            request.KernelIdleTimeout,
            request.NodeIdleTimeout,
            request.KernelRequirements,
            request.WheelContents,
            request.EnvironmentVariables,
            request.Secrets,
            cancellationToken);
}
