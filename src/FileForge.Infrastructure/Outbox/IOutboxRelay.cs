namespace FileForge.Infrastructure.Outbox;

/// <summary>Executa um ciclo de publicação do Outbox. Exposto para testes e para o host; o agendamento fica no <c>BackgroundService</c>.</summary>
public interface IOutboxRelay
{
    Task<int> ProcessarLoteAsync(CancellationToken cancellationToken);
}
