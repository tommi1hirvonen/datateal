using DuckHouse.Core.Nodes;

namespace DuckHouse.Orchestrator.Core.Entities;

public class JobNodePoolConfig : NodePoolConfig
{
    public JobNodePoolConfig() => PoolType = NodePoolType.Job;

    /// <summary>
    /// Number of idle standby nodes to keep running and ready for immediate use.
    /// <c>0</c> = on-demand provisioning (default behaviour).
    /// Warm nodes count toward <see cref="MaxNodes"/>.
    /// </summary>
    public int WarmNodes { get; set; } = 0;

    /// <summary>
    /// Hard cap on the total number of simultaneously running nodes for this pool
    /// (standby + in-use). <c>null</c> = no limit.
    /// </summary>
    public int? MaxNodes { get; set; }

    /// <summary>
    /// How long a job task waits for an available node when <see cref="MaxNodes"/>
    /// is reached. <c>null</c> = wait indefinitely (bounded only by job cancellation).
    /// </summary>
    public TimeSpan? NodeAcquireTimeout { get; set; }
}
