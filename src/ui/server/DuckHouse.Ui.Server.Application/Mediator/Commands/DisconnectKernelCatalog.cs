using DuckHouse.Core.Catalogs;
using DuckHouse.Core.Kernels;
using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record DisconnectKernelCatalogCommand(string NodeName, string KernelId, string CatalogName)
    : IRequest<ExecutionHandle>;

internal class DisconnectKernelCatalogHandler(IKernelRepository kernelRepository)
    : IRequestHandler<DisconnectKernelCatalogCommand, ExecutionHandle>
{
    public Task<ExecutionHandle> Handle(DisconnectKernelCatalogCommand request, CancellationToken cancellationToken)
    {
        var script = CatalogSetupGenerator.GenerateDetachScript(request.CatalogName);
        return kernelRepository.StartExecuteAsync(request.NodeName, request.KernelId, new ExecuteRequest(script), cancellationToken);
    }
}
