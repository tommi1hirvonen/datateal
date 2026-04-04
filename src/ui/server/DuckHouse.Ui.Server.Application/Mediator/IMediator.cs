namespace DuckHouse.Ui.Application.Mediator;

/// <summary>
/// Mediator registered in DI as a scoped service
/// </summary>
public interface IMediator
{
    public Task SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest;

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
    
    public IRequestHandler<TRequest> GetRequestHandler<TRequest>()
        where TRequest : IRequest;

    public IRequestHandler<TRequest, TResponse> GetRequestHandler<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>;
}