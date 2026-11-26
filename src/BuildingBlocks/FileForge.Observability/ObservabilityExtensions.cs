using FileForge.Mediator.Behaviors;
using FileForge.SharedKernel.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FileForge.Observability;

/// <summary>
/// Configura OpenTelemetry (Traces, Metrics, Logs via OTLP) e Health Checks padrão para os hosts do FileForge.
/// </summary>
public static class ObservabilityExtensions
{
    public static TBuilder AddFileForgeObservability<TBuilder>(
        this TBuilder builder,
        string serviceName,
        Action<ObservabilityOptions>? configure = null)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var options = new ObservabilityOptions { ServiceName = serviceName };
        configure?.Invoke(options);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var otelBuilder = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: options.ServiceName,
                    serviceNamespace: FileForgeTelemetry.ServiceNamespace,
                    serviceVersion: options.ServiceVersion)
                .AddAttributes(
                [
                    new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName)
                ]))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(FileForgeTelemetry.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (options.EnableRuntimeMetrics)
                    metrics.AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(FileForgeTelemetry.ActivitySourceName)
                    .AddSource(MediatorTelemetry.ActivitySourceName)
                    .AddSource("RabbitMQ.Client")
                    .AddAspNetCoreInstrumentation(aspNetCore =>
                    {
                        aspNetCore.RecordException = true;
                        aspNetCore.Filter = static context => !IsHealthEndpoint(context.Request.Path);
                    })
                    .AddHttpClientInstrumentation(http =>
                    {
                        http.RecordException = true;
                    })
                    .AddNpgsql();
            });

        // Exporta via OTLP somente quando um endpoint estiver configurado (evita falhas de rede em testes).
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            otelBuilder.UseOtlpExporter();

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    private static bool IsHealthEndpoint(PathString path) =>
        path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/alive", StringComparison.OrdinalIgnoreCase);
}
