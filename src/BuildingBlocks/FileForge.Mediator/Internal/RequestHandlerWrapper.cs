using Microsoft.Extensions.DependencyInjection;

namespace FileForge.Mediator.Internal;

/// <summary>
/// O <see cref="Mediator"/> só conhece <c>IRequest&lt;TResponse&gt;</c>, não o tipo concreto da requisição.
/// Este wrapper é criado uma vez por tipo (via reflection, depois cacheado) e "recupera" o TRequest
/// para resolver o handler e os behaviors de forma fortemente tipada.
/// </summary>
internal abstract class RequestHandlerWrapper<TResponse>
{
    public abstract Task<TResponse> Handle(
        IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<TResponse> Handle(
        IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;
        var handler = serviceProvider.GetService<IRequestHandler<TRequest, TResponse>>()
            ?? throw new InvalidOperationException(
                $"Nenhum handler registrado para '{typeof(TRequest).Name}'.");

        RequestHandlerDelegate<TResponse> pipeline = () => handler.Handle(typedRequest, cancellationToken);

        // Monta a "cebola" de dentro para fora: o primeiro behavior registrado é o mais externo.
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse();
        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            pipeline = () => behavior.Handle(typedRequest, next, cancellationToken);
        }

        return pipeline();
    }
}
