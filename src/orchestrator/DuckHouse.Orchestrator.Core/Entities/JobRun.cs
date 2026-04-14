using System.Text.Json;
using System.Text.Json.Serialization;
using DuckHouse.Orchestrator.Core.Enums;

namespace DuckHouse.Orchestrator.Core.Entities;

public class JobRun
{
    public Guid Id { get; set; }
    public Guid? JobId { get; set; }
    [JsonIgnore]
    public Job? Job { get; set; }

    public string JobName { get; set; } = string.Empty;

    public JobRunStatus Status { get; set; }
    public JobRunTrigger Trigger { get; set; }
    public Guid? ScheduleId { get; set; }
    public Guid? ParentRunId { get; set; }
    [JsonIgnore]
    public JobRun? ParentRun { get; set; }
    public Guid? ParentTaskRunId { get; set; }

    [JsonIgnore]
    public string? ParametersJson { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, string>? Parameters
    {
        get => ParametersJson is null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(ParametersJson);
        set => ParametersJson = value is null ? null : JsonSerializer.Serialize(value);
    }

    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public List<TaskRun> TaskRuns { get; set; } = [];

    /// <summary>
    /// JSON snapshot of the job definition (tasks, parameters, configuration) captured at trigger time.
    /// Used by the RunCoordinator so that edits to the live job never affect an in-progress or recovered run.
    /// </summary>
    public string? SnapshotJson { get; set; }
}
