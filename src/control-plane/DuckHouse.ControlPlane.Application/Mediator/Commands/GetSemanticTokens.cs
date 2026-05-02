using DuckHouse.Core.Kernels;
using DuckHouse.Core.Mediator;
using DuckHouse.ControlPlane.Core.Services;

namespace DuckHouse.ControlPlane.Application.Mediator.Commands;

public record GetSemanticTokensRequest(string NodeName, string KernelId, SemanticTokenRequest Request) : IRequest<SemanticTokenResponse>;

internal class GetSemanticTokensHandler(INodeRuntimeClient runtimeClient) : IRequestHandler<GetSemanticTokensRequest, SemanticTokenResponse>
{
    public Task<SemanticTokenResponse> Handle(GetSemanticTokensRequest request, CancellationToken cancellationToken) =>
        runtimeClient.GetSemanticTokensAsync(request.NodeName, request.KernelId, request.Request, cancellationToken);
}
