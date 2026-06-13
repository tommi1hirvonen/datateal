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

    /// <summary>
    /// Effective owner of the job: the user who last created or modified it. Used to authorize
    /// catalog access at run time (the job runs with this identity's catalog permissions).
    /// References <see cref="Datateal.Core.Users.AppUser.Id"/>. Null only for jobs created before
    /// the effective-identity feature; such jobs fail closed when run.
    /// </summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>
    /// The user who originally created the job. Audit only; never used for access decisions.
    /// References <see cref="Datateal.Core.Users.AppUser.Id"/>.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    public List<JobParameter> Parameters { get; set; } = [];
    public List<JobTask> Tasks { get; set; } = [];
    public List<JobSchedule> Schedules { get; set; } = [];

    public int TaskCount => Tasks.Count;
    public int ScheduleCount => Schedules.Count;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
