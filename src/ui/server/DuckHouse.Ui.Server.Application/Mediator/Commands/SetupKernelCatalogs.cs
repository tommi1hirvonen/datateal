using DuckHouse.Core.Catalogs;
using DuckHouse.Core.Kernels;
using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Application.Mediator.Queries;
using DuckHouse.Ui.Server.Core.Repositories;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record SetupKernelCatalogsCommand(string NodeName, string KernelId, IReadOnlyList<string> CatalogNames)
    : IRequest<ExecutionHandle>;

internal class SetupKernelCatalogsHandler(IMediator mediator, IKernelRepository kernelRepository)
    : IRequestHandler<SetupKernelCatalogsCommand, ExecutionHandle>
{
    public async Task<ExecutionHandle> Handle(SetupKernelCatalogsCommand request, CancellationToken cancellationToken)
    {
        var resolved = await mediator.SendAsync(new ResolveCatalogsRequest(request.CatalogNames), cancellationToken);
        var script = CatalogSetupGenerator.GenerateSetupScript(resolved);
        return await kernelRepository.StartExecuteAsync(request.NodeName, request.KernelId, new ExecuteRequest(script), cancellationToken);
    }
}
