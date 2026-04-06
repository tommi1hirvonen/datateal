namespace DuckHouse.Core.Kernels;

public record PollExecutionResponse(bool IsComplete, ExecutionResult? Result = null);
