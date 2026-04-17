using DuckHouse.Core.Catalogs;
using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Catalogs;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Catalogs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace DuckHouse.Ui.Server.Application.Mediator.Commands;

public record CreateCatalogRequest(
    string Name,
    bool IsManaged,
    string? DataPath,
    string? StorageConnectionString,
    string? CatalogHost,
    int? CatalogPort,
    string? CatalogDatabase,
    string? CatalogUser,
    string? CatalogPassword) : IRequest<CatalogDto>;

internal class CreateCatalogHandler(
    ICatalogRepository repository,
    ICatalogDatabaseService databaseService,
    IDataProtectionProvider dataProtection,
    IOptions<CatalogSettings> settings)
    : IRequestHandler<CreateCatalogRequest, CatalogDto>
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("DuckHouse.Catalogs");

    public async Task<CatalogDto> Handle(CreateCatalogRequest request, CancellationToken cancellationToken)
    {
        CatalogNameValidationException.Validate(request.Name);

        if (await repository.CatalogNameExistsAsync(request.Name, ct: cancellationToken))
            throw new CatalogNameConflictException(request.Name);

        var opts = settings.Value;
        var now = DateTime.UtcNow;

        Catalog catalog;

        if (request.IsManaged)
        {
            // Managed catalogs: only Name and IsManaged are persisted.
            // All connection info and the data path are derived from CatalogSettings at runtime.
            catalog = new Catalog
            {
                Id = Guid.CreateVersion7(),
                Name = request.Name,
                IsManaged = true,
                CreatedAt = now,
                UpdatedAt = now,
            };

            await databaseService.CreateDatabaseAsync(
                request.Name, opts.CatalogHost, opts.CatalogPort, opts.CatalogUser, opts.CatalogPassword, cancellationToken);
        }
        else
        {
            var dataPath = request.DataPath
                ?? throw new InvalidOperationException("DataPath is required for external catalogs.");
            var catalogHost = request.CatalogHost
                ?? throw new InvalidOperationException("CatalogHost is required for external catalogs.");
            var catalogPort = request.CatalogPort
                ?? throw new InvalidOperationException("CatalogPort is required for external catalogs.");
            var catalogDatabase = request.CatalogDatabase
                ?? throw new InvalidOperationException("CatalogDatabase is required for external catalogs.");
            var catalogUser = request.CatalogUser
                ?? throw new InvalidOperationException("CatalogUser is required for external catalogs.");

            catalog = new Catalog
            {
                Id = Guid.CreateVersion7(),
                Name = request.Name,
                IsManaged = false,
                DataPath = dataPath,
                EncryptedStorageConnectionString = !string.IsNullOrEmpty(request.StorageConnectionString)
                    ? _protector.Protect(request.StorageConnectionString)
                    : null,
                CatalogHost = catalogHost,
                CatalogPort = catalogPort,
                CatalogDatabase = catalogDatabase,
                CatalogUser = catalogUser,
                EncryptedCatalogPassword = !string.IsNullOrEmpty(request.CatalogPassword)
                    ? _protector.Protect(request.CatalogPassword)
                    : null,
                CreatedAt = now,
                UpdatedAt = now,
            };
        }

        try
        {
            await repository.CreateAsync(catalog, cancellationToken);
        }
        catch
        {
            // Compensating transaction: drop the database if entity creation fails
            if (request.IsManaged)
            {
                try
                {
                    await databaseService.DropDatabaseAsync(
                        request.Name, opts.CatalogHost, opts.CatalogPort, opts.CatalogUser, opts.CatalogPassword, cancellationToken);
                }
                catch
                {
                    // Best effort cleanup
                }
            }

            throw;
        }

        return ToDto(catalog, opts);
    }

    internal static CatalogDto ToDto(Catalog c, CatalogSettings? settings = null)
    {
        if (c.IsManaged && settings is not null)
        {
            return new CatalogDto(
                c.Id, c.Name, true,
                DataPath: settings.BaseDataPath.TrimEnd('/') + "/" + c.Name,
                CatalogHost: settings.CatalogHost,
                CatalogPort: settings.CatalogPort,
                CatalogDatabase: c.Name,
                CatalogUser: settings.CatalogUser,
                HasStorageConnectionString: !string.IsNullOrEmpty(settings.StorageConnectionString),
                HasCatalogPassword: !string.IsNullOrEmpty(settings.CatalogPassword),
                c.CreatedAt, c.UpdatedAt);
        }

        return new CatalogDto(
            c.Id, c.Name, c.IsManaged,
            c.DataPath, c.CatalogHost, c.CatalogPort, c.CatalogDatabase, c.CatalogUser,
            c.EncryptedStorageConnectionString is not null,
            c.EncryptedCatalogPassword is not null,
            c.CreatedAt, c.UpdatedAt);
    }
}
