namespace DuckHouse.ControlPlane.Api.Nodes.Aks;

public class AksNodeOptions
{
    public const string Section = "NodeService:Aks";

    public string SubscriptionId { get; set; } = "";
    public string ResourceGroupName { get; set; } = "";
    public string ClusterName { get; set; } = "";
    public string DefaultVmSize { get; set; } = "Standard_D4as_v5";
    public string RuntimeImage { get; set; } = "";
    public string NodeSubnetId { get; set; } = "";

    // Service principal authentication.
    // When all three are set, ClientSecretCredential is used.
    // Otherwise falls back to DefaultAzureCredential (managed identity, Azure CLI, etc.).
    // Store ClientSecret in a secrets manager, not in appsettings.json.
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    public bool HasServicePrincipal =>
        !string.IsNullOrEmpty(TenantId) &&
        !string.IsNullOrEmpty(ClientId) &&
        !string.IsNullOrEmpty(ClientSecret);
}
