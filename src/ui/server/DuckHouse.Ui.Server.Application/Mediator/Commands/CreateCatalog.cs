using DuckHouse.Core.Catalogs;
using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Catalogs;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Catalogs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record CreateManagedCatalogCommand(string Name) : IRequest<ManagedCatalogDto>;

public record CreateUnmanagedCatalogCommand(
    string Name,
    string DataPath,
    string? StorageConnectionString,
    string CatalogHost,
    int CatalogPort,
    string CatalogDatabase,
    string CatalogUser,
    string? CatalogPassword) : IRequest<UnmanagedCatalogDto>;

internal class CreateManagedCatalogHandler(
    ICatalogRepository repository,
    ICatalogDatabaseService databaseService,
    IOptions<CatalogSettings> settings)
    : IRequestHandler<CreateManagedCatalogCommand, ManagedCatalogDto>
{
    public async Task<ManagedCatalogDto> Handle(CreateManagedCatalogCommand request, CancellationToken cancellationToken)
    {
        CatalogNameValidationException.Validate(request.Name);

        if (await repository.CatalogNameExistsAsync(request.Name, ct: cancellationToken))
            throw new CatalogNameConflictException(request.Name);

        var opts = settings.Value;
        var now = DateTime.UtcNow;

        var catalog = new ManagedCatalog
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await databaseService.CreateDatabaseAsync(
            request.Name, opts.CatalogHost, opts.CatalogPort, opts.CatalogUser, opts.CatalogPassword, cancellationToken);

        try
        {
            await repository.CreateAsync(catalog, cancellationToken);
        }
        catch
        {
            try
            {
                await databaseService.DropDatabaseAsync(
                    request.Name, opts.CatalogHost, opts.CatalogPort, opts.CatalogUser, opts.CatalogPassword, cancellationToken);
            }
            catch { /* Best effort cleanup */ }
            throw;
        }

        return CatalogDtoMapper.ToDto(catalog, opts);
    }
}

internal class CreateUnmanagedCatalogHandler(
    ICatalogRepository repository,
    IDataProtectionProvider dataProtection)
    : IRequestHandler<CreateUnmanagedCatalogCommand, UnmanagedCatalogDto>
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("DuckHouse.Catalogs");

    public async Task<UnmanagedCatalogDto> Handle(CreateUnmanagedCatalogCommand request, CancellationToken cancellationToken)
    {
        CatalogNameValidationException.Validate(request.Name);

        if (await repository.CatalogNameExistsAsync(request.Name, ct: cancellationToken))
            throw new CatalogNameConflictException(request.Name);

        var now = DateTime.UtcNow;

        var catalog = new UnmanagedCatalog
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            DataPath = request.DataPath,
            EncryptedStorageConnectionString = !string.IsNullOrEmpty(request.StorageConnectionString)
                ? _protector.Protect(request.StorageConnectionString)
                : null,
            CatalogHost = request.CatalogHost,
            CatalogPort = request.CatalogPort,
            CatalogDatabase = request.CatalogDatabase,
            CatalogUser = request.CatalogUser,
            EncryptedCatalogPassword = !string.IsNullOrEmpty(request.CatalogPassword)
                ? _protector.Protect(request.CatalogPassword)
                : null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await repository.CreateAsync(catalog, cancellationToken);
        return CatalogDtoMapper.ToDto(catalog);
    }
}

/// <summary>Maps domain catalog entities to DTOs.</summary>
internal static class CatalogDtoMapper
{
    internal static CatalogDto ToDto(Catalog catalog, CatalogSettings? settings = null) =>
        catalog switch
        {
            ManagedCatalog m when settings is not null => ToDto(m, settings),
            UnmanagedCatalog u => ToDto(u),
            _ => throw new InvalidOperationException($"Unknown catalog type: {catalog.GetType().Name}")
        };

    internal static ManagedCatalogDto ToDto(ManagedCatalog c, CatalogSettings settings) =>
        new(c.Id, c.Name,
            DataPath: settings.BaseDataPath.TrimEnd('/') + "/" + c.Name,
            CatalogHost: settings.CatalogHost,
            CatalogPort: settings.CatalogPort,
            CatalogDatabase: c.Name,
            CatalogUser: settings.CatalogUser,
            HasStorageConnectionString: !string.IsNullOrEmpty(settings.StorageConnectionString),
            HasCatalogPassword: !string.IsNullOrEmpty(settings.CatalogPassword),
            c.CreatedAt, c.UpdatedAt);

    internal static UnmanagedCatalogDto ToDto(UnmanagedCatalog c) =>
        new(c.Id, c.Name,
            c.DataPath, c.CatalogHost, c.CatalogPort, c.CatalogDatabase, c.CatalogUser,
            HasStorageConnectionString: c.EncryptedStorageConnectionString is not null,
            HasCatalogPassword: c.EncryptedCatalogPassword is not null,
            c.CreatedAt, c.UpdatedAt);
}
