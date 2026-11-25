using FileForge.Infrastructure.Persistence;
using FileForge.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FileForge.Infrastructure.Outbox;

/// <summary>
/// Relay do Outbox. A cada ciclo lê um lote de mensagens pendentes, publica cada uma e marca como publicada
/// somente depois do sucesso. Falha em uma mensagem incrementa <c>Tentativas</c> e não bloqueia o lote.
/// Garantia: at-least-once. Se o processo cair entre publicar e marcar, a mensagem sai de novo.
/// </summary>
internal sealed class OutboxRelay(
    IServiceScopeFactory escopos,
    IDateTimeProvider relogio,
    ILogger<OutboxRelay> logger) : BackgroundService, IOutboxRelay
{
    private const int _tamanhoDoLote = 50;
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessarLoteAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.FalhaNoLote(ex);
            }

            await Task.Delay(Intervalo, stoppingToken).ConfigureAwait(false);
        }
    }

    public async Task<int> ProcessarLoteAsync(CancellationToken cancellationToken)
    {
        await using var escopo = escopos.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<FileForgeDbContext>();
        var publicador = escopo.ServiceProvider.GetRequiredService<IMensagePublicador>();

        var lote = await db.OutboxMessages
            .Where(m => m.PublicadaEmUtc == null)
            .OrderBy(m => m.CriadaEmUtc)
            .Take(_tamanhoDoLote)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var publicadas = 0;
        foreach (var mensagem in lote)
        {
            try
            {
                await publicador.PublicarAsync(mensagem, cancellationToken).ConfigureAwait(false);
                mensagem.MarcarPublicada(relogio.UtcNow);
                publicadas++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                mensagem.RegistrarFalha();
                logger.FalhaNaPublicacao(ex, mensagem.Id, mensagem.Tipo);
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return publicadas;
    }
}
