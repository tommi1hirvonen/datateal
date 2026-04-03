namespace DuckHouse.ControlPlane.Api.Nodes.Kernels;

public record KernelInfo(string Id, string Status, DateTimeOffset CreatedAt, DateTimeOffset LastActivity);

public record ExecuteRequest(string Code, double Timeout = 60.0);

public record Output(string Type, string? Name, string? Text, Dictionary<string, object>? Data, int? ExecutionCount);

public record ErrorInfo(string Ename, string Evalue, IReadOnlyList<string> Traceback);

public record ExecutionResult(string Status, int? ExecutionCount, IReadOnlyList<Output> Outputs, ErrorInfo? Error);
