using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FileForge.Infrastructure.Outbox;

/// <summary>Registra o relay do Outbox. Chamado só pelo host que publica (o Worker), nunca pela API.</summary>
public static class OutboxServiceCollectionExtensions
{
    public static IServiceCollection AddOutboxRelay(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMensagePublicador, PublicadorEmMemoria>();
        services.AddSingleton<IOutboxRelay, OutboxRelay>();
        services.AddHostedService(sp => (OutboxRelay)sp.GetRequiredService<IOutboxRelay>());

        return services;
    }
}
