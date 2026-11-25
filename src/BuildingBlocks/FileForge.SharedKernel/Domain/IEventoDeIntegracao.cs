namespace FileForge.SharedKernel.Domain;

/// <summary>
/// Marca um evento de domínio que deve ser publicado para fora do aggregate (outro serviço, fila, Worker).
/// Eventos sem esta marca ficam só em memória e não entram no Outbox.
/// </summary>
public interface IEventoDeIntegracao : IDomainEvent;
