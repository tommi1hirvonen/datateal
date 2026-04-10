namespace DuckHouse.Ui.Shared.Orchestration;

public record JobRunDetail(
    Guid Id,
    Guid JobId,
    string JobName,
    string Status,
    string Trigger,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    Dictionary<string, string>? Parameters,
    List<TaskRunDto> TaskRuns);
