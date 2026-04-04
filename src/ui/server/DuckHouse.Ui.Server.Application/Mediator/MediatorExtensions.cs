using Microsoft.Extensions.DependencyInjection;

namespace DuckHouse.Ui.Application.Mediator;

public static class MediatorExtensions
{
    public static IServiceCollection AddRequestHandlers<TScanEntryPoint>(this IServiceCollection services)
    {
        var commandHandlerType = typeof(IRequestHandler<>);
        var commandHandlers = typeof(TScanEntryPoint).Assembly
            .GetTypes()
            .Where(t =>
                t is { IsClass: true, IsAbstract: false } &&
                t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == commandHandlerType));
        foreach (var type in commandHandlers)
        {
            var @interface = type.GetInterfaces()
                .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == commandHandlerType);
            services.AddTransient(@interface, type);
        }
        
        var requestHandlerType = typeof(IRequestHandler<,>);
        var requestHandlers = typeof(TScanEntryPoint).Assembly
            .GetTypes()
            .Where(t =>
                t is { IsClass: true, IsAbstract: false } &&
                t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == requestHandlerType));
        foreach (var type in requestHandlers)
        {
            var @interface = type.GetInterfaces()
                .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == requestHandlerType);
            services.AddTransient(@interface, type);
        }
        return services;
    }
}