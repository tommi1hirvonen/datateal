using Datateal.Core.Catalogs;
using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Catalogs;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Catalogs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Datateal.Ui.Server.Application.Mediator.Commands;

public record UpdateManagedCatalogCommand(Guid Id, string Name, bool? ParquetV2, bool? PerThreadOutput) : IRequest<ManagedCatalogDto?>;

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
    ICatalogDatabaseService databaseService,
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
        if (updated is not ManagedCatalog updatedManaged) return null;

        if (request.ParquetV2.HasValue || request.PerThreadOutput.HasValue)
        {
            var opts = settings.Value;
            var dataPath = opts.BaseDataPath.TrimEnd('/') + "/" + updatedManaged.Name;
            await databaseService.SetDuckLakeSettingsAsync(
                opts.CatalogHost, opts.CatalogPort, updatedManaged.Name, opts.CatalogUser, opts.CatalogPassword,
                dataPath, !string.IsNullOrEmpty(opts.StorageConnectionString) ? opts.StorageConnectionString : null,
                updatedManaged.Name,
                request.ParquetV2 ?? false, request.PerThreadOutput ?? false,
                cancellationToken);
        }

        return CatalogDtoMapper.ToDto(updatedManaged, settings.Value);
    }
}

internal class UpdateUnmanagedCatalogHandler(
    ICatalogRepository repository,
    IDataProtectionProvider dataProtection)
    : IRequestHandler<UpdateUnmanagedCatalogCommand, UnmanagedCatalogDto?>
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("Datateal.Catalogs");

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
