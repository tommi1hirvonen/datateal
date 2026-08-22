using Datateal.Core.Orchestration;

namespace Datateal.Orchestrator.Core.Entities;

public class NotebookTask : JobTask
{
    public NotebookTask() { TaskType = TaskType.Notebook; }

    /// <summary>
    /// Workspace-relative path of the referenced notebook (e.g. <c>etl/load_sales</c>),
    /// resolved to a live <c>Notebook</c> id fresh on every use. Storing a path instead of a
    /// persisted id keeps the reference stable across workspace bundle deployments, which
    /// recreate the underlying row (with a new id) when a notebook is moved/renamed.
    /// </summary>
    public string NotebookPath { get; set; } = "";
    public string? NodePoolRef { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
}
