using DuckHouse.Core.Orchestration;

namespace DuckHouse.Ui.Shared.Orchestration;

public record TaskRunDto(
    Guid Id,
    Guid TaskId,
    string TaskName,
    TaskType TaskType,
    string Status,
    int AttemptNumber,
    string? NodeName,
    string? KernelId,
    string? ErrorMessage,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    double? DurationMs,
    Dictionary<string, string>? Parameters,
    List<string> DependencyTaskNames);
