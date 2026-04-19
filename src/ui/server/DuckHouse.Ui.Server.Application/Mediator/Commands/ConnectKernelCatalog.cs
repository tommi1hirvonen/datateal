using DuckHouse.Core.Catalogs;
using DuckHouse.Core.Kernels;
using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Application.Mediator.Queries;
using DuckHouse.Ui.Server.Core.Repositories;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record ConnectKernelCatalogCommand(string NodeName, string KernelId, string CatalogName)
    : IRequest<ExecutionHandle>;

internal class ConnectKernelCatalogHandler(IMediator mediator, IKernelRepository kernelRepository)
    : IRequestHandler<ConnectKernelCatalogCommand, ExecutionHandle>
{
    public async Task<ExecutionHandle> Handle(ConnectKernelCatalogCommand request, CancellationToken cancellationToken)
    {
        var resolved = await mediator.SendAsync(new ResolveCatalogsRequest([request.CatalogName]), cancellationToken);
        if (resolved.Count == 0)
            throw new InvalidOperationException($"Catalog '{request.CatalogName}' not found.");

        var script = CatalogSetupGenerator.GenerateAttachScript(resolved[0], isLinux: true);
        return await kernelRepository.StartExecuteAsync(request.NodeName, request.KernelId, new ExecuteRequest(script), cancellationToken);
    }
}
