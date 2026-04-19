using DuckHouse.Core.Orchestration;

namespace DuckHouse.Ui.Shared.Orchestration;

public record JobRunSummary(
    Guid Id,
    Guid JobId,
    string JobName,
    JobRunStatus Status,
    JobRunTrigger Trigger,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    Dictionary<string, string>? Parameters);
