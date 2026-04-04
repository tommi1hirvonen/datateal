using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Server.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace DuckHouse.Ui.Server.Infrastructure;

public static class ServiceExtensions
{
    public static void AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<INodeRepository, NodeRepository>();
    }
}