using DuckHouse.Core.Kernels;
using DuckHouse.Core.Mediator;
using DuckHouse.ControlPlane.Core.Services;

namespace DuckHouse.ControlPlane.Application.Mediator.Commands;

public record GetHoverInfoRequest(string NodeName, string KernelId, HoverInfoRequest Request) : IRequest<HoverInfoResponse>;

internal class GetHoverInfoHandler(INodeRuntimeClient runtimeClient) : IRequestHandler<GetHoverInfoRequest, HoverInfoResponse>
{
    public Task<HoverInfoResponse> Handle(GetHoverInfoRequest request, CancellationToken cancellationToken) =>
        runtimeClient.HoverAsync(request.NodeName, request.KernelId, request.Request, cancellationToken);
}
