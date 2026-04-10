using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Commands;

public record UpdateNodePoolConfigRequest(
    Guid Id,
    string Name,
    string VmSize,
    TimeSpan? KernelIdleTimeout,
    TimeSpan? NodeIdleTimeout,
    string? KernelRequirements,
    string? Description) : IRequest<NodePoolConfig?>;

internal class UpdateNodePoolConfigHandler(INodePoolConfigRepository repository)
    : IRequestHandler<UpdateNodePoolConfigRequest, NodePoolConfig?>
{
    public async Task<NodePoolConfig?> Handle(UpdateNodePoolConfigRequest request, CancellationToken cancellationToken)
    {
        var config = new NodePoolConfig
        {
            Id = request.Id,
            Name = request.Name,
            VmSize = request.VmSize,
            KernelIdleTimeout = request.KernelIdleTimeout,
            NodeIdleTimeout = request.NodeIdleTimeout,
            KernelRequirements = request.KernelRequirements,
            Description = request.Description,
            UpdatedAt = DateTime.UtcNow,
        };

        return await repository.UpdateAsync(config, cancellationToken);
    }
}
