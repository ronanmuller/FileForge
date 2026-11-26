using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace FileForge.Observability.Health;

/// <summary>
/// Mapeia os endpoints padrão de readiness (<c>/health</c>) e liveness (<c>/alive</c>).
/// </summary>
public static class HealthCheckExtensions
{
    public static WebApplication MapFileForgeHealthChecks(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Readiness: verifica todas as dependências registradas (PostgreSQL, RabbitMQ, Redis, etc.).
        app.MapHealthChecks("/health");

        // Liveness: verifica apenas se o processo responde, sem derrubar o container por falha externa.
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = static registration => registration.Tags.Contains("live")
        });

        return app;
    }
}
