namespace FileForge.Mediator;

/// <summary>Envia uma requisição para o seu único handler.</summary>
public interface ISender
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}

/// <summary>Publica uma notificação para todos os seus handlers.</summary>
public interface IPublisher
{
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}

/// <summary>ISP: quem só envia depende de <see cref="ISender"/>, quem só publica depende de <see cref="IPublisher"/>.</summary>
public interface IMediator : ISender, IPublisher;
