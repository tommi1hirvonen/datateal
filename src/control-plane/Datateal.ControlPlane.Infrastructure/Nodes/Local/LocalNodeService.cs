using Datateal.ControlPlane.Core.Services;
using Datateal.ControlPlane.Infrastructure.Nodes.Kernels;
using Datateal.Core.Nodes;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Datateal.ControlPlane.Infrastructure.Nodes.Local;

// Uses Kubernetes pods to simulate compute nodes when running against Docker Desktop.
public sealed class LocalNodeService : INodeService
{
    private const string Namespace = "default";
    private const string ManagedByLabelKey = "app.kubernetes.io/managed-by";
    private const string ManagedByLabelValue = "datateal-control-plane";

    private readonly IKubernetes _kubernetes;
    private readonly LocalNodeOptions _options;
    private readonly RuntimeAuthOptions _runtimeAuth;
    private readonly ILogger<LocalNodeService> _logger;

    public LocalNodeService(
        IKubernetes kubernetes,
        IOptions<LocalNodeOptions> options,
        IOptions<RuntimeAuthOptions> runtimeAuth,
        ILogger<LocalNodeService> logger)
    {
        _kubernetes = kubernetes;
        _options = options.Value;
        _runtimeAuth = runtimeAuth.Value;
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

        // Mount a host directory into the pod for persistent data storage (local dev only).
        // Requires DataVolumeHostPath and DataVolumeMountPath to both be configured.
        var hasDataVolume = !string.IsNullOrEmpty(_options.DataVolumeHostPath) &&
                            !string.IsNullOrEmpty(_options.DataVolumeMountPath);
        if (hasDataVolume)
        {
            volumes.Add(new V1Volume
            {
                Name = "data",
                HostPath = new V1HostPathVolumeSource
                {
                    Path = _options.DataVolumeHostPath,
                    Type = "DirectoryOrCreate",
                },
            });
            volumeMounts.Add(new V1VolumeMount
            {
                Name = "data",
                MountPath = _options.DataVolumeMountPath,
            });
        }

        // Build container environment variables
        var envVars = new List<V1EnvVar>();

        if (!string.IsNullOrWhiteSpace(request.KernelRequirements))
            envVars.Add(new V1EnvVar { Name = "KERNEL_PACKAGES", Value = request.KernelRequirements });

        if (request.EnvironmentVariables is { Count: > 0 })
        {
            foreach (var (key, value) in request.EnvironmentVariables)
                envVars.Add(new V1EnvVar { Name = key, Value = value });
        }

        // Create Kubernetes Secret for sensitive values
        if (request.Secrets is { Count: > 0 })
        {
            var secretName = $"env-{request.Name}";
            await CreateKubernetesSecretAsync(secretName, request.Secrets, cancellationToken);

            foreach (var key in request.Secrets.Keys)
            {
                envVars.Add(new V1EnvVar
                {
                    Name = key,
                    ValueFrom = new V1EnvVarSource
                    {
                        SecretKeyRef = new V1SecretKeySelector
                        {
                            Name = secretName,
                            Key = key,
                        },
                    },
                });
            }

            // Tell the runtime which env var names are secrets so it can redact their
            // values from cell outputs before they reach the control plane.
            // Environment var and secret keys cannot contain commas (due to validation),
            // so we can safely join them into a single string.
            envVars.Add(new V1EnvVar
            {
                Name = "DATATEAL_SECRET_KEYS",
                Value = string.Join(',', request.Secrets.Keys),
            });
        }

        // Inject runtime API key via a dedicated Kubernetes Secret (defense-in-depth auth).
        if (!string.IsNullOrEmpty(_runtimeAuth.ApiKey))
        {
            var apiKeySecretName = $"runtime-auth-{request.Name}";
            await CreateKubernetesSecretAsync(apiKeySecretName,
                new Dictionary<string, string> { ["RUNTIME_API_KEY"] = _runtimeAuth.ApiKey },
                cancellationToken);

            envVars.Add(new V1EnvVar
            {
                Name = "RUNTIME_API_KEY",
                ValueFrom = new V1EnvVarSource
                {
                    SecretKeyRef = new V1SecretKeySelector
                    {
                        Name = apiKeySecretName,
                        Key = "RUNTIME_API_KEY",
                    },
                },
            });
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
                        Image = "datateal-runtime:latest",
                        // Never pull from a registry; use the locally built image.
                        ImagePullPolicy = "Never",
                        Ports = [new V1ContainerPort { ContainerPort = 8000 }],
                        Env = envVars.Count > 0 ? envVars : null,
                        VolumeMounts = volumeMounts.Count > 0 ? volumeMounts : null,
                        // When a host-path data volume is mounted, the container must run as root
                        // so it can write to the host directory (local dev only). Without this,
                        // the non-root Dockerfile USER would deny writes to the mapped path.
                        SecurityContext = hasDataVolume ? new V1SecurityContext { RunAsUser = 0 } : null,
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

        if (request.Secrets is { Count: > 0 })
        {
            await SetSecretOwnerReferenceAsync($"env-{request.Name}", created, cancellationToken);
        }

        if (!string.IsNullOrEmpty(_runtimeAuth.ApiKey))
        {
            await SetSecretOwnerReferenceAsync($"runtime-auth-{request.Name}", created, cancellationToken);
        }

        return new NodeInfo(
            Name: created.Metadata.Name,
            ProvisioningState: created.Status?.Phase ?? "Pending",
            VmSize: null,
            PowerState: null,
            State: NodeState.Creating);
    }

    public async Task RemoveNodeAsync(string name, CancellationToken cancellationToken = default)
    {
        await _kubernetes.CoreV1.DeleteNamespacedPodAsync(name, Namespace, cancellationToken: cancellationToken);
        _logger.LogInformation("Deleted pod {PodName} from namespace {Namespace}", name, Namespace);
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

    private async Task CreateKubernetesSecretAsync(
        string secretName,
        IReadOnlyDictionary<string, string> secrets,
        CancellationToken cancellationToken)
    {
        var secret = new V1Secret
        {
            Metadata = new V1ObjectMeta
            {
                Name = secretName,
                NamespaceProperty = Namespace,
            },
            StringData = new Dictionary<string, string>(secrets),
        };

        await _kubernetes.CoreV1.CreateNamespacedSecretAsync(secret, Namespace, cancellationToken: cancellationToken);
        _logger.LogInformation("Created Secret {SecretName} with {Count} key(s)", secretName, secrets.Count);
    }

    private async Task SetSecretOwnerReferenceAsync(string secretName, V1Pod pod, CancellationToken cancellationToken)
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

        var secret = await _kubernetes.CoreV1.ReadNamespacedSecretAsync(secretName, Namespace, cancellationToken: cancellationToken);
        secret.Metadata.OwnerReferences = [ownerRef];
        await _kubernetes.CoreV1.ReplaceNamespacedSecretAsync(secret, secretName, Namespace, cancellationToken: cancellationToken);
    }
}
