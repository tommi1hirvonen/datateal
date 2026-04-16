using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Core.RuntimePackages;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record UploadWheelPackageRequest(string Name, string FileName, byte[] Data) : IRequest<WheelPackage>;

internal class UploadWheelPackageHandler(IWheelPackageRepository repository)
    : IRequestHandler<UploadWheelPackageRequest, WheelPackage>
{
    public async Task<WheelPackage> Handle(UploadWheelPackageRequest request, CancellationToken cancellationToken)
    {
        if (await repository.ExistsByNameAsync(request.Name, cancellationToken))
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

        return await repository.AddAsync(package, cancellationToken);
    }
}
