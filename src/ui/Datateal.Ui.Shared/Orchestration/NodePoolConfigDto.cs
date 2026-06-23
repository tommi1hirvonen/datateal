using Datateal.Core.Nodes;

namespace Datateal.Ui.Shared.Orchestration;

public record NodePoolConfigDto(
    Guid Id,
    string Name,
    NodePoolType PoolType,
    string VmSize,
    TimeSpan? KernelIdleTimeout,
    TimeSpan? NodeIdleTimeout,
    string? KernelRequirements,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<Guid>? WheelPackageIds,
    IReadOnlyList<Guid>? EnvironmentVariableIds,
    IReadOnlyList<Guid>? SecretIds,
    int WarmNodes = 0,
    int? MaxNodes = null,
    TimeSpan? NodeAcquireTimeout = null);

public record CreateNodePoolRequest(
    string Name,
    NodePoolType PoolType,
    string VmSize,
    TimeSpan? KernelIdleTimeout,
    TimeSpan? NodeIdleTimeout,
    string? KernelRequirements,
    string? Description,
    IReadOnlyList<Guid>? WheelPackageIds = null,
    IReadOnlyList<Guid>? EnvironmentVariableIds = null,
    IReadOnlyList<Guid>? SecretIds = null,
    int WarmNodes = 0,
    int? MaxNodes = null,
    TimeSpan? NodeAcquireTimeout = null);

public record UpdateNodePoolRequest(
    string Name,
    string VmSize,
    TimeSpan? KernelIdleTimeout,
    TimeSpan? NodeIdleTimeout,
    string? KernelRequirements,
    string? Description,
    IReadOnlyList<Guid>? WheelPackageIds = null,
    IReadOnlyList<Guid>? EnvironmentVariableIds = null,
    IReadOnlyList<Guid>? SecretIds = null,
    int WarmNodes = 0,
    int? MaxNodes = null,
    TimeSpan? NodeAcquireTimeout = null,
    bool RestartNodes = false);
