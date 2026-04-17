using DuckHouse.Ui.Server.Core.Catalogs;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Server.Infrastructure.Catalogs;
using DuckHouse.Ui.Server.Infrastructure.Data;
using DuckHouse.Ui.Server.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DuckHouse.Ui.Server.Infrastructure;

public static class ServiceExtensions
{
    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var baseAddress = configuration["ControlPlane:BaseAddress"]
            ?? throw new InvalidOperationException("ControlPlane:BaseAddress is not configured.");

        services.AddHttpClient<INodeRepository, NodeRepository>(
            client => client.BaseAddress = new Uri(baseAddress));
        services.AddHttpClient<IKernelRepository, KernelRepository>(
            client => client.BaseAddress = new Uri(baseAddress));

        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IWheelPackageRepository, WheelPackageRepository>();
        services.AddScoped<IEnvironmentRepository, EnvironmentRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICatalogDatabaseService, CatalogDatabaseService>();
        services.AddScoped<ICatalogMetadataService, CatalogMetadataService>();
    }
}