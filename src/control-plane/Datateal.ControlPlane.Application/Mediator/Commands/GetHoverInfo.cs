using Datateal.ControlPlane.Core.Services;
using Datateal.Core.Kernels;
using Datateal.Core.Mediator;

namespace Datateal.ControlPlane.Application.Mediator.Commands;

public record GetHoverInfoRequest(string NodeName, string KernelId, HoverInfoRequest Request) : IRequest<HoverInfoResponse>;

internal class GetHoverInfoHandler(INodeRuntimeClient runtimeClient) : IRequestHandler<GetHoverInfoRequest, HoverInfoResponse>
{
    public Task<HoverInfoResponse> Handle(GetHoverInfoRequest request, CancellationToken cancellationToken) =>
        runtimeClient.HoverAsync(request.NodeName, request.KernelId, request.Request, cancellationToken);
}
