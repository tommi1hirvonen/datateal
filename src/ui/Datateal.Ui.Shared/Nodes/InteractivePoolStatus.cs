using Datateal.Core.Nodes;

namespace Datateal.Ui.Shared.Nodes;

/// <summary>
/// User-facing status of an interactive node pool.
/// </summary>
/// <remarks>
/// Interactive pools always have at most one underlying compute node. Users interact with
/// them exclusively via <b>Start</b> and <b>Stop</b> — the low-level infrastructure
/// lifecycle (<see cref="NodeState"/>) is therefore an implementation detail. This enum
/// aggregates those raw states into concepts that are meaningful from the user's
/// perspective:
/// <list type="table">
///   <listheader><term>Status</term><description>Meaning</description></listheader>
///   <item><term>Stopped</term><description>No node exists for this pool. The pool can be started.</description></item>
///   <item><term>Starting</term><description>A node is being provisioned. The pool will be running shortly.</description></item>
///   <item><term>Running</term><description>The node is live and ready to accept kernel connections.</description></item>
///   <item><term>Stopping</term><description>The node is being torn down following a Stop request or idle eviction.</description></item>
///   <item><term>Error</term><description>The node reached a failure state. The pool should be stopped and restarted.</description></item>
///   <item><term>Unknown</term><description>The node exists but its state could not be determined (transient; typically resolves on the next refresh).</description></item>
/// </list>
/// </remarks>
public enum InteractivePoolStatus
{
    /// <summary>No underlying node — the pool is idle and can be started.</summary>
    Stopped,

    /// <summary>The node is being provisioned; Start has been requested.</summary>
    Starting,

    /// <summary>The node is live and accepting kernel connections.</summary>
    Running,

    /// <summary>The node is being torn down; Stop has been requested or idle eviction is in progress.</summary>
    Stopping,

    /// <summary>The node reached a failure state and requires a restart.</summary>
    Error,

    /// <summary>The node exists but its state is temporarily indeterminate.</summary>
    Unknown
}

/// <summary>
/// Extension methods that map raw <see cref="NodeState"/> values to the user-facing
/// <see cref="InteractivePoolStatus"/> aggregate.
/// </summary>
public static class NodeStateExtensions
{
    /// <summary>
    /// Converts a non-nullable <see cref="NodeState"/> to the corresponding
    /// <see cref="InteractivePoolStatus"/>.
    /// </summary>
    public static InteractivePoolStatus ToInteractivePoolStatus(this NodeState state) => state switch
    {
        NodeState.Running => InteractivePoolStatus.Running,
        NodeState.Creating => InteractivePoolStatus.Starting,
        NodeState.Deleting => InteractivePoolStatus.Stopping,
        NodeState.Failure => InteractivePoolStatus.Error,
        NodeState.Unknown => InteractivePoolStatus.Unknown,
        _ => InteractivePoolStatus.Unknown,
    };

    /// <summary>
    /// Converts a nullable <see cref="NodeState"/> to the corresponding
    /// <see cref="InteractivePoolStatus"/>. A <see langword="null"/> value means no node
    /// exists, so it maps to <see cref="InteractivePoolStatus.Stopped"/>.
    /// </summary>
    public static InteractivePoolStatus ToInteractivePoolStatus(this NodeState? state) =>
        state?.ToInteractivePoolStatus() ?? InteractivePoolStatus.Stopped;
}
