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

        string dataPath;
        string? encryptedStorageConnectionString = null;
        string catalogHost;
        int catalogPort;
        string catalogDatabase;
        string catalogUser;
        string? encryptedCatalogPassword = null;

        if (request.IsManaged)
        {
            dataPath = opts.BaseDataPath.TrimEnd('/') + "/" + request.Name;
            if (!string.IsNullOrEmpty(opts.StorageConnectionString))
                encryptedStorageConnectionString = _protector.Protect(opts.StorageConnectionString);
            catalogHost = opts.CatalogHost;
            catalogPort = opts.CatalogPort;
            catalogDatabase = request.Name;
            catalogUser = opts.CatalogUser;
            if (!string.IsNullOrEmpty(opts.CatalogPassword))
                encryptedCatalogPassword = _protector.Protect(opts.CatalogPassword);
        }
        else
        {
            dataPath = request.DataPath
                ?? throw new InvalidOperationException("DataPath is required for external catalogs.");
            if (!string.IsNullOrEmpty(request.StorageConnectionString))
                encryptedStorageConnectionString = _protector.Protect(request.StorageConnectionString);
            catalogHost = request.CatalogHost
                ?? throw new InvalidOperationException("CatalogHost is required for external catalogs.");
            catalogPort = request.CatalogPort
                ?? throw new InvalidOperationException("CatalogPort is required for external catalogs.");
            catalogDatabase = request.CatalogDatabase
                ?? throw new InvalidOperationException("CatalogDatabase is required for external catalogs.");
            catalogUser = request.CatalogUser
                ?? throw new InvalidOperationException("CatalogUser is required for external catalogs.");
            if (!string.IsNullOrEmpty(request.CatalogPassword))
                encryptedCatalogPassword = _protector.Protect(request.CatalogPassword);
        }

        // Create the PostgreSQL database for managed catalogs
        if (request.IsManaged)
        {
            await databaseService.CreateDatabaseAsync(
                catalogDatabase, catalogHost, catalogPort, catalogUser, opts.CatalogPassword, cancellationToken);
        }

        var catalog = new Catalog
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            IsManaged = request.IsManaged,
            DataPath = dataPath,
            EncryptedStorageConnectionString = encryptedStorageConnectionString,
            CatalogHost = catalogHost,
            CatalogPort = catalogPort,
            CatalogDatabase = catalogDatabase,
            CatalogUser = catalogUser,
            EncryptedCatalogPassword = encryptedCatalogPassword,
            CreatedAt = now,
            UpdatedAt = now,
        };

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
                        catalogDatabase, catalogHost, catalogPort, catalogUser, opts.CatalogPassword, cancellationToken);
                }
                catch
                {
                    // Best effort cleanup
                }
            }

            throw;
        }

        return ToDto(catalog);
    }

    internal static CatalogDto ToDto(Catalog c) => new(
        c.Id, c.Name, c.IsManaged,
        c.DataPath, c.CatalogHost, c.CatalogPort, c.CatalogDatabase, c.CatalogUser,
        c.EncryptedStorageConnectionString is not null,
        c.EncryptedCatalogPassword is not null,
        c.CreatedAt, c.UpdatedAt);
}
