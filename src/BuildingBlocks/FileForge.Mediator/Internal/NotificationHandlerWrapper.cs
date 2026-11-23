using Microsoft.Extensions.DependencyInjection;

namespace FileForge.Mediator.Internal;

/// <summary>
/// Mesmo princípio do <see cref="RequestHandlerWrapper{TResponse}"/>: a reflection acontece uma única vez
/// por tipo de notificação; depois a chamada aos handlers é fortemente tipada (sem <c>MethodInfo.Invoke</c>).
/// </summary>
internal abstract class NotificationHandlerWrapper
{
    public abstract Task Handle(
        INotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

internal sealed class NotificationHandlerWrapperImpl<TNotification> : NotificationHandlerWrapper
    where TNotification : INotification
{
    /// <summary>
    /// Executa os handlers em sequência. Se algum falhar, os demais continuam sendo executados
    /// e, ao final, as falhas são lançadas juntas em uma <see cref="AggregateException"/>.
    /// </summary>
    public override async Task Handle(
        INotification notification, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var typedNotification = (TNotification)notification;
        List<Exception>? exceptions = null;

        foreach (var handler in serviceProvider.GetServices<INotificationHandler<TNotification>>())
        {
            try
            {
                await handler.Handle(typedNotification, cancellationToken);
            }
            catch (Exception exception)
            {
                (exceptions ??= []).Add(exception);
            }
        }

        if (exceptions is not null)
            throw new AggregateException(
                $"Um ou mais handlers de '{typeof(TNotification).Name}' falharam.", exceptions);
    }
}
