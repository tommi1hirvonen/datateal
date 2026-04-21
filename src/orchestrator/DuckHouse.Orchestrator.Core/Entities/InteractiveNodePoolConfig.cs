using DuckHouse.Core.Nodes;

namespace DuckHouse.Orchestrator.Core.Entities;

public class InteractiveNodePoolConfig : NodePoolConfig
{
    public InteractiveNodePoolConfig() => PoolType = NodePoolType.Interactive;

    /// <summary>
    /// Returns the deterministic K8s node name derived from the pool ID.
    /// Stable across renames: "i" + first 11 hex chars of the pool GUID.
    /// </summary>
    public string GetNodeName() => "i" + Id.ToString("N")[..11].ToLowerInvariant();
}
