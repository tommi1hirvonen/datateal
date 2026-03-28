namespace DuckHouse.ControlPlane.Api.Nodes.Aks;

public class AksNodeOptions
{
    public const string Section = "NodeService:Aks";

    public string SubscriptionId { get; set; } = "";
    public string ResourceGroupName { get; set; } = "";
    public string ClusterName { get; set; } = "";
    public string DefaultVmSize { get; set; } = "Standard_D4as_v5";
}
