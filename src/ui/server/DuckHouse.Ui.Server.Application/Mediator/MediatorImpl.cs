using Microsoft.Extensions.DependencyInjection;

namespace DuckHouse.Ui.Application.Mediator;

/// <summary>
/// Custom implementation of a dispatcher in the mediator pattern.
/// This type is registered as a scoped service in DI.
/// </summary>
internal class MediatorImpl(IServiceProvider serviceProvider) : IMediator
{
    public Task SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        // Get the corresponding handler for the request.
        var handlerType = typeof(IRequestHandler<>).MakeGenericType(request.GetType());
        var handler = (IRequestHandler)serviceProvider.GetRequiredService(handlerType);
        return (Task)handler.Handle(request, cancellationToken);
    }

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        // Get the corresponding handler for the request.
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
        var handler = (IRequestHandler)serviceProvider.GetRequiredService(handlerType);
        return (Task<TResponse>)handler.Handle(request, cancellationToken);
    }

    public IRequestHandler<TRequest> GetRequestHandler<TRequest>()
        where TRequest : IRequest
    {
        var handlerType = typeof(IRequestHandler<>).MakeGenericType(typeof(TRequest));
        var handler = (IRequestHandler<TRequest>)serviceProvider.GetRequiredService(handlerType);
        return handler;
    }

    public IRequestHandler<TRequest, TResponse> GetRequestHandler<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
    {
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(typeof(TRequest), typeof(TResponse));
        var handler = (IRequestHandler<TRequest, TResponse>)serviceProvider.GetRequiredService(handlerType);
        return handler;
    }
}
