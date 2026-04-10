namespace DuckHouse.Ui.Shared.Orchestration;

public record TaskRunDto(
    Guid Id,
    Guid TaskId,
    string TaskName,
    string TaskType,
    string Status,
    int AttemptNumber,
    string? NodeName,
    string? KernelId,
    string? ErrorMessage,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    double? DurationMs);
