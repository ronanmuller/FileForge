using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FileForge.SharedKernel.Diagnostics;

/// <summary>
/// Ponto central de telemetria nativa do FileForge (<see cref="ActivitySource"/> e <see cref="Meter"/>).
/// Permite que Application e Infrastructure emitam spans e métricas sem acoplar a pacotes do OpenTelemetry.
/// </summary>
public static class FileForgeTelemetry
{
    public const string ServiceNamespace = "FileForge";
    public const string ActivitySourceName = "FileForge";
    public const string MeterName = "FileForge";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    // Instrumentos compartilhados entre Application, Infrastructure e Worker
    public static readonly Counter<long> SolicitacoesCriadas =
        Meter.CreateCounter<long>(
            name: "fileforge.solicitacoes.criadas",
            unit: "{solicitacao}",
            description: "Total de solicitações de extrato processadas na entrada (sucesso ou rejeição de negócio).");

    public static readonly Histogram<double> OutboxLoteDuracaoMs =
        Meter.CreateHistogram<double>(
            name: "fileforge.outbox.lote.duracao",
            unit: "ms",
            description: "Duração do processamento de um lote de mensagens pelo OutboxRelay.");

    public static readonly Counter<long> OutboxMensagensPublicadas =
        Meter.CreateCounter<long>(
            name: "fileforge.outbox.mensagens.publicadas",
            unit: "{mensagem}",
            description: "Total de mensagens do Outbox publicadas com sucesso.");

    public static readonly Counter<long> OutboxMensagensFalhas =
        Meter.CreateCounter<long>(
            name: "fileforge.outbox.mensagens.falhas",
            unit: "{mensagem}",
            description: "Total de falhas ao tentar publicar mensagens do Outbox.");
}
