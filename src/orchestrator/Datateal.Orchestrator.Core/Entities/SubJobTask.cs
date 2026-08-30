using Datateal.Core.Orchestration;

namespace Datateal.Orchestrator.Core.Entities;

public class SubJobTask : JobTask
{
    public SubJobTask() { TaskType = TaskType.SubJob; }

    /// <summary>
    /// Name of the referenced job, resolved to a live <c>Job</c> id fresh on every use (via
    /// <see cref="Datateal.Orchestrator.Core.Repositories.IJobRepository.GetJobByNameAsync"/>).
    /// Storing a name instead of a persisted id keeps the reference stable across a job rename —
    /// mirrors <c>NotebookTask.NotebookPath</c>/<c>SqlQueryTask.QueryPath</c>.
    /// </summary>
    public string SubJobName { get; set; } = "";
    public Dictionary<string, string>? Parameters { get; set; }
}
