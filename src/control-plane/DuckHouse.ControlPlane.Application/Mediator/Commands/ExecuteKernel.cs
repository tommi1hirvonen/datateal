using DuckHouse.Core.Kernels;
using DuckHouse.Core.Mediator;
using DuckHouse.ControlPlane.Core.Services;

namespace DuckHouse.ControlPlane.Application.Mediator.Commands;

public record ExecuteKernelRequest(string NodeName, string KernelId, string Code, double? Timeout = null) : IRequest<ExecutionHandle>;

internal class ExecuteKernelHandler(INodeRuntimeClient runtimeClient) : IRequestHandler<ExecuteKernelRequest, ExecutionHandle>
{
    public Task<ExecutionHandle> Handle(ExecuteKernelRequest request, CancellationToken cancellationToken) =>
        runtimeClient.StartExecuteAsync(request.NodeName, request.KernelId, new ExecuteRequest(request.Code, request.Timeout), cancellationToken);
}
