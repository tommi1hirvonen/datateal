using DuckHouse.Core.Kernels;
using DuckHouse.Ui.Shared.Kernels;

namespace DuckHouse.Ui.Client.Services;

public interface IKernelService
{
    Task<IReadOnlyList<KernelInfo>> GetKernelsAsync(string nodeName, CancellationToken cancellationToken = default);
    Task<KernelInfo> CreateKernelAsync(string nodeName, CancellationToken cancellationToken = default);
    Task<KernelInfo> GetKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default);
    Task DeleteKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default);
    Task<ExecutionHandle> StartExecuteAsync(string nodeName, string kernelId, ExecuteKernelRequest request, CancellationToken cancellationToken = default);
    Task<PollExecutionResponse> PollExecutionAsync(string nodeName, string kernelId, string executionId, CancellationToken cancellationToken = default);
    Task<KernelInfo> RestartKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default);
    Task InterruptKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default);
    Task<CompleteResponse> CompleteAsync(string nodeName, string kernelId, CompleteRequest request, CancellationToken cancellationToken = default);
    Task<DiagnoseResponse> DiagnoseAsync(string nodeName, string kernelId, DiagnoseRequest request, CancellationToken cancellationToken = default);
    Task<SemanticTokenResponse> GetSemanticTokensAsync(string nodeName, string kernelId, SemanticTokenRequest request, CancellationToken cancellationToken = default);
    Task<HoverInfoResponse> GetHoverInfoAsync(string nodeName, string kernelId, HoverInfoRequest request, CancellationToken cancellationToken = default);
}
