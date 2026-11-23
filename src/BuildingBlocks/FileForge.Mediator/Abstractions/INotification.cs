namespace FileForge.Mediator;

/// <summary>Marca uma mensagem que pode ter zero ou vários handlers (ex.: eventos de domínio).</summary>
public interface INotification;

public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}
