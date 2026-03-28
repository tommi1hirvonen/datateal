using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerService;
using Azure.ResourceManager.ContainerService.Models;
using Microsoft.Extensions.Options;

namespace DuckHouse.ControlPlane.Api.Nodes.Aks;

public sealed class AksNodeService : INodeService
{
    private readonly ArmClient _armClient;
    private readonly AksNodeOptions _options;
    private readonly ILogger<AksNodeService> _logger;

    public AksNodeService(ArmClient armClient, IOptions<AksNodeOptions> options, ILogger<AksNodeService> logger)
    {
        _armClient = armClient;
        _options = options.Value;
        _logger = logger;
    }

    private ContainerServiceManagedClusterResource GetClusterResource()
    {
        var clusterId = ContainerServiceManagedClusterResource.CreateResourceIdentifier(
            _options.SubscriptionId,
            _options.ResourceGroupName,
            _options.ClusterName);

        return _armClient.GetContainerServiceManagedClusterResource(clusterId);
    }

    public async Task<IReadOnlyList<NodeInfo>> ListNodesAsync(CancellationToken cancellationToken = default)
    {
        var cluster = GetClusterResource();
        var nodes = new List<NodeInfo>();

        await foreach (var pool in cluster.GetContainerServiceAgentPools().GetAllAsync(cancellationToken: cancellationToken))
        {
            if (pool.Data.Mode != AgentPoolMode.User)
                continue;

            nodes.Add(new NodeInfo(
                pool.Data.Name,
                pool.Data.ProvisioningState ?? "Unknown",
                pool.Data.VmSize,
                pool.Data.PowerStateCode?.ToString()));
        }

        return nodes;
    }

    public async Task<NodeInfo> CreateNodeAsync(CreateNodeRequest request, CancellationToken cancellationToken = default)
    {
        var vmSize = request.VmSize ?? _options.DefaultVmSize;
        var cluster = GetClusterResource();

        var nodePoolData = new ContainerServiceAgentPoolData
        {
            Count = 1,
            VmSize = vmSize,
            OSType = ContainerServiceOSType.Linux,
            OSSku = ContainerServiceOSSku.Ubuntu,
            Mode = AgentPoolMode.User,
        };

        // Node pool provisioning is long-running; start the operation and return immediately.
        await cluster.GetContainerServiceAgentPools().CreateOrUpdateAsync(
            WaitUntil.Started,
            request.Name,
            nodePoolData,
            cancellationToken);

        _logger.LogInformation("Started provisioning node pool {PoolName} with VM size {VmSize}", request.Name, vmSize);

        return new NodeInfo(request.Name, "Creating", vmSize, PowerState: "Running");
    }

    public async Task RemoveNodeAsync(string name, CancellationToken cancellationToken = default)
    {
        var cluster = GetClusterResource();
        var pool = await cluster.GetContainerServiceAgentPoolAsync(name, cancellationToken);
        await pool.Value.DeleteAsync(WaitUntil.Started, cancellationToken);
        _logger.LogInformation("Started deletion of node pool {PoolName}", name);
    }

    public Task StopNodeAsync(string name, CancellationToken cancellationToken = default) =>
        SetPowerStateAsync(name, ContainerServiceStateCode.Stopped, cancellationToken);

    public Task StartNodeAsync(string name, CancellationToken cancellationToken = default) =>
        SetPowerStateAsync(name, ContainerServiceStateCode.Running, cancellationToken);

    private async Task SetPowerStateAsync(string name, ContainerServiceStateCode state, CancellationToken cancellationToken)
    {
        var cluster = GetClusterResource();
        var pool = (await cluster.GetContainerServiceAgentPoolAsync(name, cancellationToken)).Value;
        var existing = pool.Data;

        var updated = new ContainerServiceAgentPoolData
        {
            Count = existing.Count,
            VmSize = existing.VmSize,
            OSType = existing.OSType,
            OSSku = existing.OSSku,
            Mode = existing.Mode,
            PowerStateCode = state,
        };

        await cluster.GetContainerServiceAgentPools().CreateOrUpdateAsync(
            WaitUntil.Started,
            name,
            updated,
            cancellationToken);

        _logger.LogInformation("Set power state of node pool {PoolName} to {State}", name, state);
    }
}

