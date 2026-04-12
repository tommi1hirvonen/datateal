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
    string? Description,
    List<Guid>? WheelPackageIds = null) : IRequest<NodePoolConfig>;

internal class CreateNodePoolConfigHandler(INodePoolConfigRepository repository)
    : IRequestHandler<CreateNodePoolConfigRequest, NodePoolConfig>
{
    private const int MaxNodePoolNameLength = 63 - 13; // 63 k8s limit minus "job-XXXXXXXX-" prefix

    public async Task<NodePoolConfig> Handle(CreateNodePoolConfigRequest request, CancellationToken cancellationToken)
    {
        if (request.Name.Length > MaxNodePoolNameLength)
            throw new ArgumentException(
                $"Node pool name must be {MaxNodePoolNameLength} characters or fewer " +
                $"(the name is embedded in job-run node names which have a 63-character Kubernetes limit).",
                nameof(request.Name));

        var config = new NodePoolConfig
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            VmSize = request.VmSize,
            KernelIdleTimeout = request.KernelIdleTimeout,
            NodeIdleTimeout = request.NodeIdleTimeout,
            KernelRequirements = request.KernelRequirements,
            Description = request.Description,
            WheelPackageIds = request.WheelPackageIds,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        return await repository.CreateAsync(config, cancellationToken);
    }
}
