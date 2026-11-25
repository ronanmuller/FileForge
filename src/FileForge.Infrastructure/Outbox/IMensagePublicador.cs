using System.Collections.Concurrent;

namespace FileForge.Infrastructure.Outbox;

/// <summary>
/// Destino da publicação. Hoje não há broker: a implementação em memória serve para estudar o fluxo.
/// Trocar por RabbitMQ/Service Bus é criar outra implementação, sem mexer no relay.
/// </summary>
public interface IMensagePublicador
{
    Task PublicarAsync(OutboxMessage mensagem, CancellationToken cancellationToken);
}

public sealed class PublicadorEmMemoria : IMensagePublicador
{
    private readonly ConcurrentQueue<OutboxMessage> _publicadas = new();

    public IReadOnlyCollection<OutboxMessage> Publicadas => _publicadas.ToArray();

    public Task PublicarAsync(OutboxMessage mensagem, CancellationToken cancellationToken)
    {
        _publicadas.Enqueue(mensagem);
        return Task.CompletedTask;
    }
}
