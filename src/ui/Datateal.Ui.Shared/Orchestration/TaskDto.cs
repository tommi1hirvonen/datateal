using Datateal.Core.Orchestration;

namespace Datateal.Ui.Shared.Orchestration;

public record TaskDto(
    Guid Id,
    string Name,
    TaskType TaskType,
    int MaxRetries,
    TimeSpan RetryInterval,
    TimeSpan? Timeout,
    string? NotebookPath,
    string? QueryPath,
    string? SubJobName,
    string? NodePoolRef,
    Dictionary<string, string>? Parameters,
    List<TaskDependencyDto> Dependencies);

public record TaskDependencyDto(Guid DependsOnTaskId, string DependsOnTaskName, string Condition);
