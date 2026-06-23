using Datateal.ControlPlane.Core.Services;
using Datateal.Core.Kernels;
using Datateal.Core.Mediator;

namespace Datateal.ControlPlane.Application.Mediator.Commands;

public record DiagnoseKernelRequest(string NodeName, string KernelId, DiagnoseRequest Request) : IRequest<DiagnoseResponse>;

internal class DiagnoseKernelHandler(INodeRuntimeClient runtimeClient) : IRequestHandler<DiagnoseKernelRequest, DiagnoseResponse>
{
    public Task<DiagnoseResponse> Handle(DiagnoseKernelRequest request, CancellationToken cancellationToken) =>
        runtimeClient.DiagnoseAsync(request.NodeName, request.KernelId, request.Request, cancellationToken);
}
