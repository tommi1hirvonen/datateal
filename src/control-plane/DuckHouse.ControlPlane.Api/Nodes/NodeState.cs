namespace DuckHouse.ControlPlane.Api.Nodes;

public enum NodeState
{
    Unknown,
    Stopped,
    Resuming,
    Running,
    Stopping,
    Deleting,
    Creating,
    Failure
}