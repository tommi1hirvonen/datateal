namespace DuckHouse.ControlPlane.Core.Nodes;

public class NodeConfig
{
    public string NodeName { get; set; } = "";

    public TimeSpan KernelIdleTimeout { get; set; }

    public TimeSpan NodeIdleTimeout { get; set; }
}
