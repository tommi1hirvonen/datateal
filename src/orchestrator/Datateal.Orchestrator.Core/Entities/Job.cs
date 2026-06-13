namespace Datateal.Orchestrator.Core.Entities;

public class Job
{
    public Guid Id { get; set; }

    /// <summary>Owning workspace.</summary>
    public Guid WorkspaceId { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid? FolderId { get; set; }
    public int MaxConcurrentRuns { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;

    public List<JobParameter> Parameters { get; set; } = [];
    public List<JobTask> Tasks { get; set; } = [];
    public List<JobSchedule> Schedules { get; set; } = [];

    public int TaskCount => Tasks.Count;
    public int ScheduleCount => Schedules.Count;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
