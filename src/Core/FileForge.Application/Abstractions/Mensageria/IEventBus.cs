using FileForge.SharedKernel.Domain;

namespace FileForge.Application.Abstractions.Mensageria;

/// <summary>
/// Publica eventos de integração (para fora do processo: RabbitMQ na etapa 11).
/// A Application depende só desta interface; o broker real fica na Infrastructure.
/// </summary>
/// <remarks>
/// Os handlers <b>não</b> chamam isto diretamente após salvar (dupla escrita!). O caminho é:
/// aggregate gera evento → Outbox grava na mesma transação → publicador do Outbox chama <see cref="IEventBus"/>.
/// </remarks>
public interface IEventBus
{
    Task PublicarAsync(IDomainEvent evento, CancellationToken cancellationToken);
}
