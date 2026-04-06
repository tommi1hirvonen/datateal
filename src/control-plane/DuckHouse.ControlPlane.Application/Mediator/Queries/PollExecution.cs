using DuckHouse.Core.Kernels;
using DuckHouse.Core.Mediator;
using DuckHouse.ControlPlane.Core.Services;

namespace DuckHouse.ControlPlane.Application.Mediator.Queries;

public record PollExecutionRequest(string NodeName, string KernelId, string ExecutionId) : IRequest<PollExecutionResponse>;

internal class PollExecutionHandler(INodeRuntimeClient runtimeClient) : IRequestHandler<PollExecutionRequest, PollExecutionResponse>
{
    public Task<PollExecutionResponse> Handle(PollExecutionRequest request, CancellationToken cancellationToken) =>
        runtimeClient.PollExecutionAsync(request.NodeName, request.KernelId, request.ExecutionId, cancellationToken);
}
