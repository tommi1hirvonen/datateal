using DuckHouse.Core.Catalogs;
using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Catalogs;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Catalogs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record UpdateManagedCatalogCommand(Guid Id, string Name) : IRequest<ManagedCatalogDto?>;

public record UpdateUnmanagedCatalogCommand(
    Guid Id,
    string Name,
    string? DataPath,
    string? StorageConnectionString,
    string? CatalogHost,
    int? CatalogPort,
    string? CatalogDatabase,
    string? CatalogUser,
    string? CatalogPassword) : IRequest<UnmanagedCatalogDto?>;

internal class UpdateManagedCatalogHandler(
    ICatalogRepository repository,
    IOptions<CatalogSettings> settings)
    : IRequestHandler<UpdateManagedCatalogCommand, ManagedCatalogDto?>
{
    public async Task<ManagedCatalogDto?> Handle(UpdateManagedCatalogCommand request, CancellationToken cancellationToken)
    {
        CatalogNameValidationException.Validate(request.Name);

        var existing = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is not ManagedCatalog managed) return null;

        if (managed.Name != request.Name &&
            await repository.CatalogNameExistsAsync(request.Name, request.Id, cancellationToken))
            throw new CatalogNameConflictException(request.Name);

        managed.Name = request.Name;

        var updated = await repository.UpdateAsync(managed, cancellationToken);
        return updated is ManagedCatalog updatedManaged
            ? CatalogDtoMapper.ToDto(updatedManaged, settings.Value)
            : null;
    }
}

internal class UpdateUnmanagedCatalogHandler(
    ICatalogRepository repository,
    IDataProtectionProvider dataProtection)
    : IRequestHandler<UpdateUnmanagedCatalogCommand, UnmanagedCatalogDto?>
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("DuckHouse.Catalogs");

    public async Task<UnmanagedCatalogDto?> Handle(UpdateUnmanagedCatalogCommand request, CancellationToken cancellationToken)
    {
        CatalogNameValidationException.Validate(request.Name);

        var existing = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is not UnmanagedCatalog unmanaged) return null;

        if (unmanaged.Name != request.Name &&
            await repository.CatalogNameExistsAsync(request.Name, request.Id, cancellationToken))
            throw new CatalogNameConflictException(request.Name);

        unmanaged.Name = request.Name;

        if (request.DataPath is not null)
            unmanaged.DataPath = request.DataPath;
        if (request.CatalogHost is not null)
            unmanaged.CatalogHost = request.CatalogHost;
        if (request.CatalogPort.HasValue)
            unmanaged.CatalogPort = request.CatalogPort.Value;
        if (request.CatalogDatabase is not null)
            unmanaged.CatalogDatabase = request.CatalogDatabase;
        if (request.CatalogUser is not null)
            unmanaged.CatalogUser = request.CatalogUser;
        if (request.StorageConnectionString is not null)
            unmanaged.EncryptedStorageConnectionString = _protector.Protect(request.StorageConnectionString);
        if (request.CatalogPassword is not null)
            unmanaged.EncryptedCatalogPassword = _protector.Protect(request.CatalogPassword);

        var updated = await repository.UpdateAsync(unmanaged, cancellationToken);
        return updated is UnmanagedCatalog updatedUnmanaged
            ? CatalogDtoMapper.ToDto(updatedUnmanaged)
            : null;
    }
}
