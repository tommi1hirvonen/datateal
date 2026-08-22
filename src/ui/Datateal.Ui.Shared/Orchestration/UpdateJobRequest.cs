using Datateal.Core.Orchestration;

namespace Datateal.Ui.Shared.Orchestration;

public record UpdateJobRequest(
    string Name,
    string? Description,
    Guid? FolderId,
    int MaxConcurrentRuns,
    bool IsEnabled,
    List<UpdateTaskRequest>? Tasks = null,
    List<UpdateParameterRequest>? Parameters = null);

public record UpdateTaskRequest(
    string Name,
    TaskType TaskType,
    int MaxRetries,
    TimeSpan RetryInterval,
    TimeSpan? Timeout,
    string? NotebookPath,
    string? QueryPath,
    Guid? SubJobId,
    string? NodePoolRef,
    Dictionary<string, string>? Parameters,
    List<UpdateDependencyRequest> Dependencies);

public record UpdateDependencyRequest(string DependsOnTaskName, string Condition);

public record UpdateParameterRequest(
    string Name,
    string? DefaultValue,
    bool IsRequired,
    string? Description);
