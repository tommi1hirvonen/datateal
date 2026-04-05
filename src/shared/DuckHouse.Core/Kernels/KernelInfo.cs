namespace DuckHouse.Core.Kernels;

public record KernelInfo(
    string Id,
    KernelStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivity);
