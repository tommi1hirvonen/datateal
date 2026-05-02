using DuckHouse.Core.Kernels;
using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record GetHoverInfoRequest(string NodeName, string KernelId, HoverInfoRequest Request) : IRequest<HoverInfoResponse>;

internal class GetHoverInfoHandler(IKernelRepository kernelRepository) : IRequestHandler<GetHoverInfoRequest, HoverInfoResponse>
{
    public Task<HoverInfoResponse> Handle(GetHoverInfoRequest request, CancellationToken cancellationToken) =>
        kernelRepository.GetHoverInfoAsync(request.NodeName, request.KernelId, request.Request, cancellationToken);
}
