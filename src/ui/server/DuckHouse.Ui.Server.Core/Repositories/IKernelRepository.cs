using DuckHouse.Core.Kernels;

namespace DuckHouse.Ui.Server.Core.Repositories;

public interface IKernelRepository
{
    Task<IReadOnlyList<KernelInfo>> GetKernelsAsync(string nodeName, CancellationToken cancellationToken = default);
    Task<KernelInfo> CreateKernelAsync(string nodeName, CancellationToken cancellationToken = default);
    Task<KernelInfo> GetKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default);
    Task DeleteKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default);
    Task<ExecutionHandle> StartExecuteAsync(string nodeName, string kernelId, ExecuteRequest request, CancellationToken cancellationToken = default);
    Task<PollExecutionResponse> PollExecutionAsync(string nodeName, string kernelId, string executionId, CancellationToken cancellationToken = default);
    Task<KernelInfo> RestartKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default);
    Task InterruptKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default);
    Task<CompleteResponse> CompleteAsync(string nodeName, string kernelId, CompleteRequest request, CancellationToken cancellationToken = default);
    Task<DiagnoseResponse> DiagnoseAsync(string nodeName, string kernelId, DiagnoseRequest request, CancellationToken cancellationToken = default);
}
