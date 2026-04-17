using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Catalogs;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Catalogs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record UpdateCatalogRequest(
    Guid Id,
    string Name,
    string? DataPath,
    string? StorageConnectionString,
    string? CatalogHost,
    int? CatalogPort,
    string? CatalogDatabase,
    string? CatalogUser,
    string? CatalogPassword) : IRequest<CatalogDto?>;

internal class UpdateCatalogHandler(
    ICatalogRepository repository,
    IDataProtectionProvider dataProtection,
    IOptions<CatalogSettings> settings)
    : IRequestHandler<UpdateCatalogRequest, CatalogDto?>
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("DuckHouse.Catalogs");

    public async Task<CatalogDto?> Handle(UpdateCatalogRequest request, CancellationToken cancellationToken)
    {
        CatalogNameValidationException.Validate(request.Name);

        var existing = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return null;

        if (existing.Name != request.Name &&
            await repository.CatalogNameExistsAsync(request.Name, request.Id, cancellationToken))
            throw new CatalogNameConflictException(request.Name);

        existing.Name = request.Name;

        if (!existing.IsManaged)
        {
            // External catalogs: update any provided connection fields
            if (request.DataPath is not null)
                existing.DataPath = request.DataPath;
            if (request.CatalogHost is not null)
                existing.CatalogHost = request.CatalogHost;
            if (request.CatalogPort.HasValue)
                existing.CatalogPort = request.CatalogPort;
            if (request.CatalogDatabase is not null)
                existing.CatalogDatabase = request.CatalogDatabase;
            if (request.CatalogUser is not null)
                existing.CatalogUser = request.CatalogUser;
            if (request.StorageConnectionString is not null)
                existing.EncryptedStorageConnectionString = _protector.Protect(request.StorageConnectionString);
            if (request.CatalogPassword is not null)
                existing.EncryptedCatalogPassword = _protector.Protect(request.CatalogPassword);
        }
        // For managed catalogs only the name is persisted; connection info comes from settings at runtime.

        var updated = await repository.UpdateAsync(existing, cancellationToken);
        return updated is null ? null : CreateCatalogHandler.ToDto(updated, settings.Value);
    }
}
