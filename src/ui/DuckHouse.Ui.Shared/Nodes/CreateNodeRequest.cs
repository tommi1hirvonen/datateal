namespace DuckHouse.Ui.Shared.Nodes;

public record CreateNodeRequest(
    string Name,
    string VmSize,
    TimeSpan? KernelIdleTimeout = null,
    TimeSpan? NodeIdleTimeout = null,
    string? KernelRequirements = null,
    IReadOnlyList<Guid>? WheelPackageIds = null);
