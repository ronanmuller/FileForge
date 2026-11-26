namespace FileForge.Observability;

/// <summary>
/// Opções de configuração da telemetria do serviço (OpenTelemetry / OTLP).
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>Nome lógico do serviço exibido nos traces, métricas e logs (ex.: "FileForge.Api").</summary>
    public string ServiceName { get; set; } = "FileForge";

    /// <summary>Versão do serviço registrada no atributo <c>service.version</c>.</summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>Ativa métricas de GC, heap, ThreadPool e contenção do runtime .NET.</summary>
    public bool EnableRuntimeMetrics { get; set; } = true;
}
