using System.Text.Json.Serialization;
using DuckHouse.Orchestrator.Core.Enums;

namespace DuckHouse.Orchestrator.Core.Entities;

public class TaskRun
{
    public Guid Id { get; set; }
    public Guid JobRunId { get; set; }
    [JsonIgnore]
    public JobRun? JobRun { get; set; }

    public Guid? TaskId { get; set; }
    [JsonIgnore]
    public JobTask? Task { get; set; }

    public string TaskName { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;

    public TaskRunStatus Status { get; set; }
    public int AttemptNumber { get; set; }
    public string? NodeName { get; set; }
    public string? KernelId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? DurationMs { get; set; }

    public string? OutputJson { get; set; }
}
