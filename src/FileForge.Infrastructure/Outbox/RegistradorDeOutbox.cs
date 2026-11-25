using System;
using System.Text.Json;
using FileForge.Infrastructure.Persistence;
using FileForge.SharedKernel.Abstractions;
using FileForge.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Infrastructure.Outbox;

/// <summary>
/// Lê os aggregates alterados no ChangeTracker e grava no Outbox apenas os eventos de integração.
/// Roda no mesmo DbContext do UnitOfWork, então a mensagem entra na mesma transação do estado.
/// Limpa todos os eventos do aggregate após registrar, para que não sejam gravados duas vezes.
/// </summary>
internal sealed class RegistradorDeOutbox(FileForgeDbContext dbContext, IDateTimeProvider relogio) : IRegistradorDeEventos
{
    public void RegistrarEventos()
    {
        var agora = relogio.UtcNow;

        // ToList antes de iterar: adicionar OutboxMessage altera o ChangeTracker.
        var agregados = dbContext.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .Select(e => e.Entity)
            .OfType<IHasDomainEvents>()
            .ToList();

        foreach (var agregado in agregados)
        {
            foreach (var evento in agregado.DomainEvents.OfType<IEventoDeIntegracao>())
            {
                dbContext.OutboxMessages.Add(new OutboxMessage(
                    Guid.NewGuid(),
                    evento.GetType().Name,
                    JsonSerializer.Serialize(evento, evento.GetType()),
                    agora));
            }

            agregado.ClearDomainEvents();
        }
    }
}
