using DuckHouse.Orchestrator.Core.Configuration;
using DuckHouse.Orchestrator.Core.Interfaces;
using DuckHouse.Orchestrator.Core.Repositories;
using DuckHouse.Orchestrator.Infrastructure.Catalogs;
using DuckHouse.Orchestrator.Infrastructure.Clients;
using DuckHouse.Orchestrator.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DuckHouse.Orchestrator.Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var controlPlaneBaseAddress = configuration["ControlPlane:BaseAddress"]
            ?? throw new InvalidOperationException("ControlPlane:BaseAddress is not configured.");

        services.AddHttpClient("ControlPlane", client =>
            client.BaseAddress = new Uri(controlPlaneBaseAddress));

        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IJobRunRepository, JobRunRepository>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
        services.AddScoped<INodePoolConfigRepository, NodePoolConfigRepository>();

        services.AddScoped<IControlPlaneClient, ControlPlaneClient>();
        services.AddScoped<IWorkspaceReader, WorkspaceReader>();
        services.AddScoped<IWheelPackageReader, WheelPackageReader>();
        services.AddScoped<IEnvironmentResolver, EnvironmentResolver>();
        services.AddScoped<ICatalogResolver, CatalogResolver>();

        services.Configure<CatalogSettings>(configuration.GetSection("Catalogs"));

        return services;
    }
}
