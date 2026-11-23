using System.Collections.Concurrent;
using FileForge.Mediator.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.Mediator;

public sealed class Mediator(IServiceProvider serviceProvider) : IMediator
{
    private static readonly ConcurrentDictionary<Type, object> RequestWrappers = new();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (RequestHandlerWrapper<TResponse>)RequestWrappers.GetOrAdd(
            request.GetType(),
            requestType => Activator.CreateInstance(
                typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse)))!);

        return wrapper.Handle(request, serviceProvider, cancellationToken);
    }

    public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        // Usa o tipo em tempo de execução para funcionar mesmo quando publicado como INotification.
        var handlerType = typeof(INotificationHandler<>).MakeGenericType(notification.GetType());
        var handleMethod = handlerType.GetMethod(nameof(INotificationHandler<INotification>.Handle))!;

        foreach (var handler in serviceProvider.GetServices(handlerType))
        {
            await (Task)handleMethod.Invoke(handler, [notification, cancellationToken])!;
        }
    }
}
