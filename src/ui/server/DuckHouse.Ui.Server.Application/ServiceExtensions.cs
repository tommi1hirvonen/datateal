using DuckHouse.Ui.Application.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace DuckHouse.Ui.Server.Application;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplicationServices()
        {
            services.AddScoped<IMediator, MediatorImpl>();
            services.AddRequestHandlers<MediatorImpl>();
            return services;
        }
    }
}