using DuckHouse.Core.Mediator;
using DuckHouse.Core.Nodes;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Nodes;

namespace DuckHouse.Ui.Server.Application.Mediator.Queries;

public record GetInteractivePoolsRequest : IRequest<IReadOnlyList<InteractivePoolDto>>;

internal class GetInteractivePoolsHandler(IInteractivePoolRepository poolRepository, INodeRepository nodeRepository)
    : IRequestHandler<GetInteractivePoolsRequest, IReadOnlyList<InteractivePoolDto>>
{
    public async Task<IReadOnlyList<InteractivePoolDto>> Handle(
        GetInteractivePoolsRequest request, CancellationToken cancellationToken)
    {
        var pools = await poolRepository.GetAllAsync(cancellationToken);

        var nodeTasks = pools.Select(p =>
            nodeRepository.GetNodeAsync(p.NodeName, cancellationToken)).ToList();

        await Task.WhenAll(nodeTasks);

        return pools
            .Zip(nodeTasks, (pool, nodeTask) =>
                new InteractivePoolDto(
                    pool.Id,
                    pool.Name,
                    pool.NodeName,
                    pool.VmSize,
                    pool.KernelIdleTimeout,
                    pool.NodeIdleTimeout,
                    pool.Description,
                    (nodeTask.Result?.State).ToInteractivePoolStatus()))
            .ToList();
    }
}
