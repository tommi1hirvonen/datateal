using Datateal.Auth.ApiKey;
using Datateal.Data.Catalogs;
using Datateal.Ui.Server.Application.Ai;
using Datateal.Ui.Server.Core.Catalogs;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Server.Infrastructure.Ai;
using Datateal.Ui.Server.Infrastructure.Catalogs;
using Datateal.Ui.Server.Infrastructure.Data;
using Datateal.Ui.Server.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Datateal.Ui.Server.Infrastructure;

public static class ServiceExtensions
{
    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var baseAddress = configuration["ControlPlane:BaseAddress"]
            ?? throw new InvalidOperationException("ControlPlane:BaseAddress is not configured.");

        var controlPlaneApiKey = configuration["ServiceAuth:ControlPlane:ApiKey"]
            ?? throw new InvalidOperationException("ServiceAuth:ControlPlane:ApiKey is not configured.");

        services.AddHttpClient<INodeRepository, NodeRepository>(
            client => client.BaseAddress = new Uri(baseAddress))
            .AddHttpMessageHandler(() => new ApiKeyDelegatingHandler(
                Options.Create(new ApiKeyDelegatingOptions { ApiKey = controlPlaneApiKey })));
        services.AddHttpClient<IKernelRepository, KernelRepository>(
            client => client.BaseAddress = new Uri(baseAddress))
            .AddHttpMessageHandler(() => new ApiKeyDelegatingHandler(
                Options.Create(new ApiKeyDelegatingOptions { ApiKey = controlPlaneApiKey })));

        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IWorkspaceManagementRepository, WorkspaceManagementRepository>();
        services.AddScoped<IWheelPackageRepository, WheelPackageRepository>();
        services.AddScoped<IEnvironmentRepository, EnvironmentRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICatalogAccessResolver, CatalogAccessResolver>();
        services.AddScoped<ICatalogAccessService, CatalogAccessService>();
        services.AddScoped<ICatalogDatabaseService, CatalogDatabaseService>();
        services.AddScoped<ICatalogMetadataService, CatalogMetadataService>();
        services.AddScoped<IInteractivePoolRepository, InteractivePoolRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IAiProviderFactory, AiProviderFactory>();
    }
}
