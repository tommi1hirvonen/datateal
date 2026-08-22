using Datateal.Core.Orchestration;

namespace Datateal.Orchestrator.Core.Entities;

public class SqlQueryTask : JobTask
{
    public SqlQueryTask() { TaskType = TaskType.SqlQuery; }

    /// <summary>
    /// Workspace-relative path of the referenced query (e.g. <c>reports/summary</c>),
    /// resolved to a live <c>Query</c> id fresh on every use. Storing a path instead of a
    /// persisted id keeps the reference stable across workspace bundle deployments, which
    /// recreate the underlying row (with a new id) when a query is moved/renamed.
    /// </summary>
    public string QueryPath { get; set; } = "";
    public string? NodePoolRef { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
}
