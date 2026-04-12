namespace DuckHouse.Core.Nodes;

public record CreateNodeRequest(
    string Name,
    string? VmSize = null,
    TimeSpan? KernelIdleTimeout = null,
    TimeSpan? NodeIdleTimeout = null,
    string? KernelRequirements = null,
    IReadOnlyList<WheelContent>? WheelContents = null);
