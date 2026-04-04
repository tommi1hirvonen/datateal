namespace DuckHouse.ControlPlane.Api.Nodes.Local;

public class LocalNodeOptions
{
    public const string Section = "NodeService:Local";

    /// <summary>
    /// The kubeconfig context to use. Defaults to "docker-desktop".
    /// Set to null to use the current-context from the kubeconfig file.
    /// </summary>
    public string? KubeContext { get; set; } = "docker-desktop";
}
