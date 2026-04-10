using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Commands;

public record CreateNodePoolConfigRequest(
    string Name,
    string VmSize,
    TimeSpan? KernelIdleTimeout,
    TimeSpan? NodeIdleTimeout,
    string? KernelRequirements,
    string? Description) : IRequest<NodePoolConfig>;

internal class CreateNodePoolConfigHandler(INodePoolConfigRepository repository)
    : IRequestHandler<CreateNodePoolConfigRequest, NodePoolConfig>
{
    public async Task<NodePoolConfig> Handle(CreateNodePoolConfigRequest request, CancellationToken cancellationToken)
    {
        var config = new NodePoolConfig
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            VmSize = request.VmSize,
            KernelIdleTimeout = request.KernelIdleTimeout,
            NodeIdleTimeout = request.NodeIdleTimeout,
            KernelRequirements = request.KernelRequirements,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        return await repository.CreateAsync(config, cancellationToken);
    }
}
