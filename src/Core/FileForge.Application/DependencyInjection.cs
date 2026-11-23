using FileForge.Application.Behaviors;
using FileForge.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.Application;

/// <summary>
/// Registro de dependências da camada Application. Cada camada expõe o seu <c>AddX()</c>;
/// a API/Worker (composition root) só encadeia: <c>services.AddApplication().AddInfrastructure(config)</c>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMediator(cfg => cfg
            .RegisterHandlersFromAssembly(typeof(DependencyInjection).Assembly)
            // Ordem = de fora para dentro. Logging é o mais externo para medir o pipeline inteiro.
            .AddOpenBehavior(typeof(LoggingBehavior<,>)));

        return services;
    }
}
