using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Application.Validation;
using DuckHouse.Orchestrator.Application.Engine;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Interfaces;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Commands;

public record UpdateNodePoolConfigRequest(
    Guid Id,
    string Name,
    string VmSize,
    TimeSpan? KernelIdleTimeout,
    TimeSpan? NodeIdleTimeout,
    string? KernelRequirements,
    string? Description,
    List<Guid>? WheelPackageIds = null,
    List<Guid>? EnvironmentVariableIds = null,
    List<Guid>? SecretIds = null,
    int WarmNodes = 0,
    int? MaxNodes = null,
    TimeSpan? NodeAcquireTimeout = null) : IRequest<NodePoolConfig?>;

internal class UpdateNodePoolConfigHandler(
    INodePoolConfigRepository repository,
    IControlPlaneClient controlPlaneClient,
    WarmPoolManager warmPoolManager,
    IWheelPackageReader wheelPackageReader,
    IEnvironmentResolver environmentResolver)
    : IRequestHandler<UpdateNodePoolConfigRequest, NodePoolConfig?>
{
    public async Task<NodePoolConfig?> Handle(UpdateNodePoolConfigRequest request, CancellationToken cancellationToken)
    {
        var nameError = NodeNameValidator.ValidateNodePoolName(request.Name);
        if (nameError is not null)
            throw new ArgumentException(nameError, nameof(request.Name));

        var existing = await repository.GetAsync(request.Id, cancellationToken);
        if (existing is null) return null;

        if (existing is JobNodePoolConfig)
        {
            if (request.MaxNodes.HasValue && request.MaxNodes.Value < 1)
                throw new ArgumentException("MaxNodes must be 1 or greater when set.", nameof(request.MaxNodes));
            if (request.MaxNodes.HasValue && request.MaxNodes.Value < request.WarmNodes)
                throw new ArgumentException(
                    $"MaxNodes ({request.MaxNodes.Value}) must be greater than or equal to WarmNodes ({request.WarmNodes}).",
                    nameof(request.MaxNodes));
        }

        existing.Name = request.Name;
        existing.VmSize = request.VmSize;
        existing.KernelIdleTimeout = request.KernelIdleTimeout;
        existing.NodeIdleTimeout = request.NodeIdleTimeout;
        existing.KernelRequirements = request.KernelRequirements;
        existing.Description = request.Description;
        existing.WheelPackageIds = request.WheelPackageIds;
        existing.EnvironmentVariableIds = request.EnvironmentVariableIds;
        existing.SecretIds = request.SecretIds;

        if (existing is JobNodePoolConfig jobConfig)
        {
            jobConfig.WarmNodes = request.WarmNodes;
            jobConfig.MaxNodes = request.MaxNodes;
            jobConfig.NodeAcquireTimeout = request.NodeAcquireTimeout;
        }

        var updated = await repository.UpdateAsync(existing, cancellationToken);

        if (existing is InteractiveNodePoolConfig interactive)
        {
            await controlPlaneClient.UpdateNodeEvictionConfigAsync(
                interactive.GetNodeName(),
                request.KernelIdleTimeout,
                request.NodeIdleTimeout,
                cancellationToken);
        }
        else if (existing is JobNodePoolConfig updatedJobConfig)
        {
            // Adjust the live warm pool state to reflect the new config
            await warmPoolManager.AdjustPoolAsync(
                updatedJobConfig, controlPlaneClient, wheelPackageReader, environmentResolver,
                cancellationToken);
        }

        return updated;
    }
}
