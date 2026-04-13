using DuckHouse.ControlPlane.Application.InactivityEviction;
using DuckHouse.ControlPlane.Core.Nodes;
using DuckHouse.ControlPlane.Core.Repositories;
using DuckHouse.ControlPlane.Core.Services;
using DuckHouse.Core.Mediator;
using DuckHouse.Core.Nodes;
using Microsoft.Extensions.Options;

namespace DuckHouse.ControlPlane.Application.Mediator.Commands;

public record CreateNodeRequest(
    string Name,
    string? VmSize = null,
    TimeSpan? KernelIdleTimeout = null,
    TimeSpan? NodeIdleTimeout = null,
    string? KernelRequirements = null,
    IReadOnlyList<WheelContent>? WheelContents = null) : IRequest<NodeInfo>;

internal class CreateNodeHandler(
    INodeService nodeService,
    INodeConfigRepository nodeConfigRepository,
    IOptions<InactivityEvictionOptions> evictionOptions) : IRequestHandler<CreateNodeRequest, NodeInfo>
{
    public async Task<NodeInfo> Handle(CreateNodeRequest request, CancellationToken cancellationToken)
    {
        var opts = evictionOptions.Value;
        var config = new NodeConfig
        {
            NodeName = request.Name,
            KernelIdleTimeout = request.KernelIdleTimeout ?? opts.KernelIdleTimeout,
            NodeIdleTimeout = request.NodeIdleTimeout ?? opts.NodeIdleTimeout,
        };

        await nodeConfigRepository.UpsertAsync(config, cancellationToken);

        var node = await nodeService.CreateNodeAsync(
            new DuckHouse.Core.Nodes.CreateNodeRequest(request.Name, request.VmSize,
                KernelRequirements: request.KernelRequirements,
                WheelContents: request.WheelContents),
            cancellationToken);

        return node;
    }
}
