namespace DuckHouse.Ui.Shared.Kernels;

public record ExecuteKernelRequest(string Code, double? Timeout = null);
