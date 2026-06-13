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
}
