using Microsoft.Extensions.Logging;

namespace FileForge.Infrastructure.Outbox;

/// <summary>Logs do Outbox. Faixa da Infrastructure: 2000-2999.</summary>
internal static partial class OutboxLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Error,
        Message = "Falha ao processar o lote do Outbox; nova tentativa no próximo ciclo.")]
    public static partial void FalhaNoLote(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning,
        Message = "Falha ao publicar a mensagem {MensagemId} ({Tipo}).")]
    public static partial void FalhaNaPublicacao(this ILogger logger, Exception ex, Guid mensagemId, string tipo);
}
