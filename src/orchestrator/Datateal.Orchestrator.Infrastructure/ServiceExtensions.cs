using Datateal.Auth.ApiKey;
using Datateal.Data.Catalogs;
using Datateal.Orchestrator.Core.Configuration;
using Datateal.Orchestrator.Core.Interfaces;
using Datateal.Orchestrator.Core.Repositories;
using Datateal.Orchestrator.Infrastructure.Catalogs;
using Datateal.Orchestrator.Infrastructure.Clients;
using Datateal.Orchestrator.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Datateal.Orchestrator.Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var controlPlaneBaseAddress = configuration["ControlPlane:BaseAddress"]
            ?? throw new InvalidOperationException("ControlPlane:BaseAddress is not configured.");

        services.Configure<ApiKeyDelegatingOptions>(configuration.GetSection("ServiceAuth:ControlPlane"));
        services.AddTransient<ApiKeyDelegatingHandler>();

        services.AddHttpClient("ControlPlane", client =>
            client.BaseAddress = new Uri(controlPlaneBaseAddress))
            .AddHttpMessageHandler<ApiKeyDelegatingHandler>();

        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IJobRunRepository, JobRunRepository>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
        services.AddScoped<INodePoolConfigRepository, NodePoolConfigRepository>();

        services.AddSingleton<IControlPlaneClient, ControlPlaneClient>();
        services.AddScoped<IWorkspaceReader, WorkspaceReader>();
        services.AddSingleton<IWheelPackageReader, WheelPackageReader>();
        services.AddSingleton<IEnvironmentResolver, EnvironmentResolver>();
        services.AddScoped<ICatalogResolver, CatalogResolver>();
        services.AddScoped<ICatalogAccessResolver, CatalogAccessResolver>();
        services.AddScoped<ICatalogAccessAuthorizer, CatalogAccessAuthorizer>();

        services.Configure<CatalogSettings>(configuration.GetSection("Catalogs"));

        return services;
    }
}
