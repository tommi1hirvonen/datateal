using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerService;
using Azure.ResourceManager.ContainerService.Models;
using DuckHouse.ControlPlane.Core.Services;
using DuckHouse.Core.Nodes;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DuckHouse.ControlPlane.Infrastructure.Nodes.Aks;

public sealed class AksNodeService : INodeService
{
    private const string Namespace = "default";
    private const string ManagedByLabelKey = "app.kubernetes.io/managed-by";
    private const string ManagedByLabelValue = "duckhouse-control-plane";

    private readonly ArmClient _armClient;
    private readonly IKubernetes _kubernetes;
    private readonly AksNodeOptions _options;
    private readonly ILogger<AksNodeService> _logger;

    public AksNodeService(ArmClient armClient, IKubernetes kubernetes, IOptions<AksNodeOptions> options, ILogger<AksNodeService> logger)
    {
        _armClient = armClient;
        _kubernetes = kubernetes;
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

    private static NodeState CalculateState(string provisioningState, string powerState) =>
        (provisioningState.ToLower(), powerState.ToLower()) switch
        {
            ("failed", _) => NodeState.Failure,
            ("deleting", _) => NodeState.Deleting,
            ("creating", _) => NodeState.Creating,
            ("starting", _) => NodeState.Resuming,
            ("stopping", _) => NodeState.Stopping,
            ("succeeded", "stopped") => NodeState.Stopped,
            ("succeeded", "running") => NodeState.Running,
            _ => NodeState.Unknown
        };

    public async Task<IReadOnlyList<NodeInfo>> ListNodesAsync(CancellationToken cancellationToken = default)
    {
        var cluster = GetClusterResource();
        var nodes = new List<NodeInfo>();

        await foreach (var pool in cluster.GetContainerServiceAgentPools().GetAllAsync(cancellationToken: cancellationToken))
        {
            if (pool.Data.Mode != AgentPoolMode.User)
                continue;

            var provisioningState = pool.Data.ProvisioningState ?? "Unknown";
            var powerState = pool.Data.PowerStateCode?.ToString() ?? "Unknown";
            var state = CalculateState(provisioningState, powerState);
            nodes.Add(new NodeInfo(
                Name: pool.Data.Name,
                ProvisioningState: provisioningState,
                VmSize: pool.Data.VmSize,
                PowerState: powerState,
                State: state));
        }

        return nodes;
    }

    public async Task<NodeInfo?> GetNodeAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            var cluster = GetClusterResource();
            var pool = (await cluster.GetContainerServiceAgentPoolAsync(name, cancellationToken)).Value;

            var provisioningState = pool.Data.ProvisioningState ?? "Unknown";
            var powerState = pool.Data.PowerStateCode?.ToString() ?? "Unknown";
            var state = CalculateState(provisioningState, powerState);
            return new NodeInfo(
                Name: pool.Data.Name,
                ProvisioningState: provisioningState,
                VmSize: pool.Data.VmSize,
                PowerState: powerState,
                State: state);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
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
            VnetSubnetId = string.IsNullOrEmpty(_options.NodeSubnetId) ? null : new ResourceIdentifier(_options.NodeSubnetId),
        };

        // Node pool provisioning is long-running; start the operation and return immediately.
        await cluster.GetContainerServiceAgentPools().CreateOrUpdateAsync(
            WaitUntil.Started,
            request.Name,
            nodePoolData,
            cancellationToken);

        _logger.LogInformation("Started provisioning node pool {PoolName} with VM size {VmSize}", request.Name, vmSize);

        var volumes = new List<V1Volume>();
        var volumeMounts = new List<V1VolumeMount>();

        if (request.WheelContents is { Count: > 0 } wheels)
        {
            await CreateWheelConfigMapsAsync(request.Name, wheels, volumes, volumeMounts, cancellationToken);
        }

        var pod = new V1Pod
        {
            Metadata = new V1ObjectMeta
            {
                Name = request.Name,
                NamespaceProperty = Namespace,
                Labels = new Dictionary<string, string>
                {
                    [ManagedByLabelKey] = ManagedByLabelValue,
                },
            },
            Spec = new V1PodSpec
            {
                // Pin the pod to the dedicated node pool so it runs on the provisioned VM.
                NodeSelector = new Dictionary<string, string>
                {
                    ["kubernetes.azure.com/agentpool"] = request.Name,
                },
                Containers =
                [
                    new V1Container
                    {
                        Name = "node",
                        Image = _options.RuntimeImage,
                        Ports = [new V1ContainerPort { ContainerPort = 8000 }],
                        Env = string.IsNullOrWhiteSpace(request.KernelRequirements)
                            ? null
                            : [new V1EnvVar { Name = "KERNEL_PACKAGES", Value = request.KernelRequirements }],
                        VolumeMounts = volumeMounts.Count > 0 ? volumeMounts : null,
                    },
                ],
                Volumes = volumes.Count > 0 ? volumes : null,
                RestartPolicy = "Always",
            },
        };

        var created = await _kubernetes.CoreV1.CreateNamespacedPodAsync(pod, Namespace, cancellationToken: cancellationToken);
        _logger.LogInformation("Created pod {PodName} in namespace {Namespace}", created.Metadata.Name, Namespace);

        if (request.WheelContents is { Count: > 0 })
        {
            await SetConfigMapOwnerReferencesAsync(request.Name, created, cancellationToken);
        }

        return new NodeInfo(
            Name: request.Name,
            ProvisioningState: "Creating",
            VmSize: vmSize,
            PowerState: "Running",
            State: NodeState.Creating);
    }

    public async Task RemoveNodeAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            await _kubernetes.CoreV1.DeleteNamespacedPodAsync(name, Namespace, cancellationToken: cancellationToken);
            _logger.LogInformation("Deleted pod {PodName} from namespace {Namespace}", name, Namespace);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete pod {PodName}; it may not exist", name);
        }

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

    private async Task CreateWheelConfigMapsAsync(
        string podName,
        IReadOnlyList<WheelContent> wheels,
        List<V1Volume> volumes,
        List<V1VolumeMount> volumeMounts,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < wheels.Count; i++)
        {
            var wheel = wheels[i];
            var configMapName = $"wheels-{podName}-{i}";
            var safeKey = WheelFileNameToKey(wheel.FileName);

            var configMap = new V1ConfigMap
            {
                Metadata = new V1ObjectMeta
                {
                    Name = configMapName,
                    NamespaceProperty = Namespace,
                },
                BinaryData = new Dictionary<string, byte[]>
                {
                    [safeKey] = wheel.Data,
                },
            };

            await _kubernetes.CoreV1.CreateNamespacedConfigMapAsync(configMap, Namespace, cancellationToken: cancellationToken);
            _logger.LogInformation("Created ConfigMap {ConfigMapName} for wheel {FileName}", configMapName, wheel.FileName);

            volumes.Add(new V1Volume
            {
                Name = $"wheel-{i}",
                ConfigMap = new V1ConfigMapVolumeSource { Name = configMapName },
            });

            volumeMounts.Add(new V1VolumeMount
            {
                Name = $"wheel-{i}",
                MountPath = $"/etc/wheels/vol-{i}",
                ReadOnlyProperty = true,
            });
        }
    }

    private async Task SetConfigMapOwnerReferencesAsync(string podName, V1Pod pod, CancellationToken cancellationToken)
    {
        var ownerRef = new V1OwnerReference
        {
            ApiVersion = "v1",
            Kind = "Pod",
            Name = pod.Metadata.Name,
            Uid = pod.Metadata.Uid,
            BlockOwnerDeletion = true,
            Controller = false,
        };

        var configMaps = await _kubernetes.CoreV1.ListNamespacedConfigMapAsync(
            Namespace,
            cancellationToken: cancellationToken);

        var prefix = $"wheels-{podName}-";
        foreach (var cm in configMaps.Items.Where(c => c.Metadata.Name.StartsWith(prefix)))
        {
            cm.Metadata.OwnerReferences = [ownerRef];
            await _kubernetes.CoreV1.ReplaceNamespacedConfigMapAsync(cm, cm.Metadata.Name, Namespace, cancellationToken: cancellationToken);
        }
    }

    private static string WheelFileNameToKey(string fileName) =>
        System.Text.RegularExpressions.Regex.Replace(fileName, @"[^a-zA-Z0-9._-]", "_");
}
