namespace DuckHouse.Core.Nodes;

public record CreateNodeRequest(
    string Name,
    string? VmSize = null,
    TimeSpan? KernelIdleTimeout = null,
    TimeSpan? NodeIdleTimeout = null);
