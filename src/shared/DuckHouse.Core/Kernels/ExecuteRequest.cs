namespace DuckHouse.Core.Kernels;

public record ExecuteRequest(string Code, double? Timeout = null);
