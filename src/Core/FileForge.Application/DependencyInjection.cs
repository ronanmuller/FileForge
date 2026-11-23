using FileForge.Application.Behaviors;
using FileForge.Mediator;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.Application;

/// <summary>
/// Registro de dependências da camada Application. Cada camada expõe o seu <c>AddX()</c>;
/// a API/Worker (composition root) só encadeia: <c>services.AddApplication().AddInfrastructure(config)</c>.
/// </summary>
/// <remarks>
/// A Application <b>consome</b>, mas não registra, <c>IUsuarioAtual</c>, <c>IDateTimeProvider</c>,
/// repositórios e <c>IUnitOfWork</c>: quem fornece é o host (API/Worker) e a Infrastructure.
/// </remarks>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediator(cfg => cfg
            .RegisterHandlersFromAssembly(assembly)
            // Ordem = de fora para dentro:
            // 1. Logging mede e registra tudo (inclusive acesso negado e validação).
            // 2. Autorização antes da validação: quem não pode, nem descobre quais campos são válidos.
            // 3. Validação de formato antes do handler.
            .AddOpenBehavior(typeof(LoggingBehavior<,>))
            .AddOpenBehavior(typeof(AuthorizationBehavior<,>))
            .AddOpenBehavior(typeof(ValidationBehavior<,>)));

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped, includeInternalTypes: true);

        return services;
    }
}
