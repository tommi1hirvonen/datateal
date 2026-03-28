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
            .Select(p => new NodeInfo(p.Metadata.Name, p.Status?.Phase ?? "Unknown"))
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
                        Image = "ubuntu:22.04",
                        Command = ["sleep", "infinity"],
                        Resources = new V1ResourceRequirements
                        {
                            Requests = new Dictionary<string, ResourceQuantity>
                            {
                                ["cpu"] = new ResourceQuantity("100m"),
                                ["memory"] = new ResourceQuantity("128Mi"),
                            },
                        },
                    },
                ],
                RestartPolicy = "Never",
            },
        };

        var created = await _kubernetes.CoreV1.CreateNamespacedPodAsync(pod, Namespace, cancellationToken: cancellationToken);
        _logger.LogInformation("Created pod {PodName} in namespace {Namespace}", created.Metadata.Name, Namespace);

        return new NodeInfo(created.Metadata.Name, created.Status?.Phase ?? "Pending");
    }

    public async Task RemoveNodeAsync(string name, CancellationToken cancellationToken = default)
    {
        await _kubernetes.CoreV1.DeleteNamespacedPodAsync(name, Namespace, cancellationToken: cancellationToken);
        _logger.LogInformation("Deleted pod {PodName} from namespace {Namespace}", name, Namespace);
    }
}
