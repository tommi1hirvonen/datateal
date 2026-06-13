namespace Datateal.Ui.Server.Core.Repositories;

/// <summary>
/// Lightweight projection of an interactive node pool config, including the derived node name.
/// </summary>
public record InteractivePoolInfo(
    Guid Id,
    string Name,
    string NodeName,
    string VmSize,
    TimeSpan? KernelIdleTimeout,
    TimeSpan? NodeIdleTimeout,
    string? KernelRequirements,
    string? Description,
    IReadOnlyList<Guid>? WheelPackageIds,
    IReadOnlyList<Guid>? EnvironmentVariableIds,
    IReadOnlyList<Guid>? SecretIds);

public interface IInteractivePoolRepository
{
    Task<InteractivePoolInfo?> GetByNameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InteractivePoolInfo>> GetAllAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> if any interactive pool in <paramref name="workspaceId"/> owns
    /// the given <paramref name="nodeName"/> (i.e., the pool's derived node name matches).
    /// </summary>
    Task<bool> HasNodeAsync(Guid workspaceId, string nodeName, CancellationToken cancellationToken = default);
}
