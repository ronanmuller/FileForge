using System.Text.Json;
using System.Text.Json;
using FileForge.Infrastructure.Outbox;
using FileForge.SharedKernel.Abstractions;
using FileForge.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FileForge.Infrastructure.Persistence;

/// <summary>
/// Implementação do Unit of Work sobre o DbContext. Antes de confirmar, converte os eventos de domínio
/// dos aggregates alterados em <see cref="OutboxMessage"/>, no mesmo contexto: estado e mensagem
/// vão na mesma transação. Também traduz a violação de unicidade (23505) para <see cref="UnicidadeVioladaException"/>.
/// </summary>
public sealed class UnitOfWork(FileForgeDbContext dbContext, IDateTimeProvider relogio) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RegistrarEventosNoOutbox();

        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new UnicidadeVioladaException(MapearRestricao(pg.ConstraintName), ex);
        }
    }

    private void RegistrarEventosNoOutbox()
    {
        var agora = relogio.UtcNow;

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

    private static RestricaoUnica MapearRestricao(string? nomeDoIndice) => nomeDoIndice switch
    {
        "ux_solicitacao_ativa" => RestricaoUnica.SolicitacaoAtiva,
        "ux_contas_agencia_numero" => RestricaoUnica.ContaAgenciaNumero,
        "ux_clientes_documento" => RestricaoUnica.ClienteDocumento,
        _ => RestricaoUnica.Desconhecida
    };
}
