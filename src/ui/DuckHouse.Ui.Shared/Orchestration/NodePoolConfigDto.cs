namespace DuckHouse.Ui.Shared.Orchestration;

public record NodePoolConfigDto(
    Guid Id,
    string Name,
    string VmSize,
    TimeSpan? KernelIdleTimeout,
    TimeSpan? NodeIdleTimeout,
    string? KernelRequirements,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<Guid>? WheelPackageIds);

public record CreateNodePoolRequest(
    string Name,
    string VmSize,
    TimeSpan? KernelIdleTimeout,
    TimeSpan? NodeIdleTimeout,
    string? KernelRequirements,
    string? Description,
    IReadOnlyList<Guid>? WheelPackageIds = null);

public record UpdateNodePoolRequest(
    string Name,
    string VmSize,
    TimeSpan? KernelIdleTimeout,
    TimeSpan? NodeIdleTimeout,
    string? KernelRequirements,
    string? Description,
    IReadOnlyList<Guid>? WheelPackageIds = null);
