using Datateal.Core.Mediator;
using Datateal.Core.Nodes;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Nodes;

namespace Datateal.Ui.Server.Application.Mediator.Queries;

public record GetInteractivePoolsRequest(Guid WorkspaceId) : IRequest<IReadOnlyList<InteractivePoolDto>>;

internal class GetInteractivePoolsHandler(IInteractivePoolRepository poolRepository, INodeRepository nodeRepository)
    : IRequestHandler<GetInteractivePoolsRequest, IReadOnlyList<InteractivePoolDto>>
{
    public async Task<IReadOnlyList<InteractivePoolDto>> Handle(
        GetInteractivePoolsRequest request, CancellationToken cancellationToken)
    {
        var pools = await poolRepository.GetAllAsync(request.WorkspaceId, cancellationToken);

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
