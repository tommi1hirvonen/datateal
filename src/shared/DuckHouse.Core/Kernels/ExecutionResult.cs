namespace DuckHouse.Core.Kernels;

public record ExecutionResult(
    string Status,
    int? ExecutionCount,
    IReadOnlyList<Output> Outputs,
    ErrorInfo? Error,
    double DurationMs);
