using k8s;
using k8s.Models;

namespace DuckHouse.ControlPlane.Api.Nodes.Local;

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

    public async Task<NodeInfo> CreateNodeAsync(CreateNodeRequest request, CancellationToken cancellationToken = default)
    {
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
                    },
                ],
                RestartPolicy = "Always",
            },
        };

        var created = await _kubernetes.CoreV1.CreateNamespacedPodAsync(pod, Namespace, cancellationToken: cancellationToken);
        _logger.LogInformation("Created pod {PodName} in namespace {Namespace}", created.Metadata.Name, Namespace);

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
}
