using Datateal.Core.Orchestration;

namespace Datateal.Ui.Client.Models;

public class EditableTask
{
    public string Name { get; set; } = "";
    public TaskType TaskType { get; set; } = TaskType.Notebook;
    public int MaxRetries { get; set; }
    public string RetryInterval { get; set; } = "00:00:30";
    public string? Timeout { get; set; }

    public string? NotebookPath { get; set; }
    public string? QueryPath { get; set; }
    public string? SubJobName { get; set; }

    public string? NodePoolRef { get; set; }
    public List<EditableTaskParameter> Parameters { get; set; } = [];
    public List<EditableDependency> Dependencies { get; set; } = [];
}

public class EditableTaskParameter
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class EditableDependency
{
    public string TaskName { get; set; } = "";
    public string Condition { get; set; } = "OnSuccess";
}

public class EditableParameter
{
    public string Name { get; set; } = "";
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
}
