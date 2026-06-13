using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Core.RuntimePackages;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record UploadWheelPackageRequest(Guid WorkspaceId, string Name, string FileName, byte[] Data) : IRequest<WheelPackage>;

internal class UploadWheelPackageHandler(IWheelPackageRepository repository)
    : IRequestHandler<UploadWheelPackageRequest, WheelPackage>
{
    public async Task<WheelPackage> Handle(UploadWheelPackageRequest request, CancellationToken cancellationToken)
    {
        if (await repository.ExistsByNameAsync(request.WorkspaceId, request.Name, cancellationToken))
            throw new InvalidOperationException($"A wheel package named '{request.Name}' already exists.");

        var package = new WheelPackage
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            FileName = request.FileName,
            Data = request.Data,
            Size = request.Data.LongLength,
            UploadedAt = DateTime.UtcNow,
        };

        return await repository.AddAsync(request.WorkspaceId, package, cancellationToken);
    }
}
