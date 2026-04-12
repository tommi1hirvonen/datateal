using DuckHouse.ControlPlane.Core.Services;
using DuckHouse.Core.Nodes;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace DuckHouse.ControlPlane.Infrastructure.Nodes.Local;

// Uses Kubernetes pods to simulate compute nodes when running against Docker Desktop.
public sealed class LocalNodeService : INodeService
{
    private const string Namespace = "default";
    private const string ManagedByLabelKey = "app.kubernetes.io/managed-by";
    private const string ManagedByLabelValue = "duckhouse-control-plane";

    private readonly IKubernetes _kubernetes;
    private readonly ILogger<LocalNodeService> _logger;

    public LocalNodeService(IKubernetes kubernetes, ILogger<LocalNodeService> logger)
    {
        _kubernetes = kubernetes;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NodeInfo>> ListNodesAsync(CancellationToken cancellationToken = default)
    {
        var pods = await _kubernetes.CoreV1.ListNamespacedPodAsync(
            Namespace,
            labelSelector: $"{ManagedByLabelKey}={ManagedByLabelValue}",
            cancellationToken: cancellationToken);

        return pods.Items
            .Select(p => new NodeInfo(
                Name: p.Metadata.Name,
                ProvisioningState: p.Status?.Phase ?? "Unknown",
                VmSize: null,
                PowerState: null,
                State: NodeState.Running))
            .ToList();
    }

    public async Task<NodeInfo?> GetNodeAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            var pod = await _kubernetes.CoreV1.ReadNamespacedPodAsync(name, Namespace, cancellationToken: cancellationToken);
            return new NodeInfo(
                Name: pod.Metadata.Name,
                ProvisioningState: pod.Status?.Phase ?? "Unknown",
                VmSize: null,
                PowerState: null,
                State: NodeState.Running);
        }
        catch (k8s.Autorest.HttpOperationException ex) when ((int)ex.Response.StatusCode == 404)
        {
            return null;
        }
    }

    public async Task<NodeInfo> CreateNodeAsync(CreateNodeRequest request, CancellationToken cancellationToken = default)
    {
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
                Containers =
                [
                    new V1Container
                    {
                        Name = "node",
                        Image = "duckhouse-runtime:latest",
                        // Never pull from a registry; use the locally built image.
                        ImagePullPolicy = "Never",
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
            Name: created.Metadata.Name,
            ProvisioningState: created.Status?.Phase ?? "Pending",
            VmSize: null,
            PowerState: null,
            State: NodeState.Resuming);
    }

    public async Task RemoveNodeAsync(string name, CancellationToken cancellationToken = default)
    {
        await _kubernetes.CoreV1.DeleteNamespacedPodAsync(name, Namespace, cancellationToken: cancellationToken);
        _logger.LogInformation("Deleted pod {PodName} from namespace {Namespace}", name, Namespace);
    }

    public Task StopNodeAsync(string name, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Stop is a no-op for local pods ({PodName})", name);
        return Task.CompletedTask;
    }

    public Task StartNodeAsync(string name, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Start is a no-op for local pods ({PodName})", name);
        return Task.CompletedTask;
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
