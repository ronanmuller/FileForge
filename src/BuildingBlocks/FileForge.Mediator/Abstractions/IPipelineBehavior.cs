namespace FileForge.Mediator;

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Middleware que envolve a execução do handler (validação, logging, idempotência, transação...).
/// Chame <paramref name="next"/> para seguir para o próximo behavior ou para o handler.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
