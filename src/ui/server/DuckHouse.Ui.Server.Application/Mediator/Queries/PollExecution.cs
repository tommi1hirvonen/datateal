using DuckHouse.Core.Kernels;
using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;

namespace DuckHouse.Ui.Server.Application.Mediator.Queries;

public record PollExecutionRequest(string NodeName, string KernelId, string ExecutionId) : IRequest<PollExecutionResponse>;

internal class PollExecutionHandler(IKernelRepository kernelRepository) : IRequestHandler<PollExecutionRequest, PollExecutionResponse>
{
    public Task<PollExecutionResponse> Handle(PollExecutionRequest request, CancellationToken cancellationToken) =>
        kernelRepository.PollExecutionAsync(request.NodeName, request.KernelId, request.ExecutionId, cancellationToken);
}
