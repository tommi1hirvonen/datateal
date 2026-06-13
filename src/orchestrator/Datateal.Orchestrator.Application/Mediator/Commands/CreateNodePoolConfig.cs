using Datateal.Core.Mediator;
using Datateal.Core.Nodes;
using Datateal.Orchestrator.Application.Validation;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Commands;

public record CreateNodePoolConfigRequest(
    string Name,
    NodePoolType PoolType,
    string VmSize,
    TimeSpan? KernelIdleTimeout,
    TimeSpan? NodeIdleTimeout,
    string? KernelRequirements,
    string? Description,
    List<Guid>? WheelPackageIds,
    List<Guid>? EnvironmentVariableIds,
    List<Guid>? SecretIds,
    int WarmNodes = 0,
    int? MaxNodes = null,
    TimeSpan? NodeAcquireTimeout = null) : IRequest<NodePoolConfig>;

internal class CreateNodePoolConfigHandler(INodePoolConfigRepository repository, Datateal.Orchestrator.Core.IWorkspaceContext workspace)
    : IRequestHandler<CreateNodePoolConfigRequest, NodePoolConfig>
{
    public async Task<NodePoolConfig> Handle(CreateNodePoolConfigRequest request, CancellationToken cancellationToken)
    {
        var nameError = NodeNameValidator.ValidateNodePoolName(request.Name);
        if (nameError is not null)
            throw new ArgumentException(nameError, nameof(request.Name));

        if (request.PoolType == NodePoolType.Job)
            ValidateWarmPoolFields(request.WarmNodes, request.MaxNodes);

        var workspaceId = workspace.RequireWorkspaceId();
        NodePoolConfig config = request.PoolType == NodePoolType.Interactive
            ? new InteractiveNodePoolConfig
            {
                WorkspaceId = workspaceId,
                Name = request.Name,
                VmSize = request.VmSize,
                KernelIdleTimeout = request.KernelIdleTimeout,
                NodeIdleTimeout = request.NodeIdleTimeout,
                KernelRequirements = request.KernelRequirements,
                Description = request.Description,
                WheelPackageIds = request.WheelPackageIds,
                EnvironmentVariableIds = request.EnvironmentVariableIds,
                SecretIds = request.SecretIds,
            }
            : new JobNodePoolConfig
            {
                WorkspaceId = workspaceId,
                Name = request.Name,
                VmSize = request.VmSize,
                KernelIdleTimeout = request.KernelIdleTimeout,
                NodeIdleTimeout = request.NodeIdleTimeout,
                KernelRequirements = request.KernelRequirements,
                Description = request.Description,
                WheelPackageIds = request.WheelPackageIds,
                EnvironmentVariableIds = request.EnvironmentVariableIds,
                SecretIds = request.SecretIds,
                WarmNodes = request.WarmNodes,
                MaxNodes = request.MaxNodes,
                NodeAcquireTimeout = request.NodeAcquireTimeout,
            };

        return await repository.CreateAsync(config, cancellationToken);
    }

    private static void ValidateWarmPoolFields(int warmNodes, int? maxNodes)
    {
        if (maxNodes.HasValue && maxNodes.Value < 1)
            throw new ArgumentException("MaxNodes must be 1 or greater when set.", nameof(maxNodes));
        if (maxNodes.HasValue && maxNodes.Value < warmNodes)
            throw new ArgumentException(
                $"MaxNodes ({maxNodes.Value}) must be greater than or equal to WarmNodes ({warmNodes}).",
                nameof(maxNodes));
    }
}
