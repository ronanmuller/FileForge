using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FileForge.Mediator;

public sealed class MediatorConfiguration
{
    internal List<Assembly> Assemblies { get; } = [];
    internal List<Type> OpenBehaviors { get; } = [];

    public MediatorConfiguration RegisterHandlersFromAssembly(Assembly assembly)
    {
        Assemblies.Add(assembly);
        return this;
    }

    /// <summary>Registra um behavior genérico aberto, ex.: <c>typeof(LoggingBehavior&lt;,&gt;)</c>. A ordem importa.</summary>
    public MediatorConfiguration AddOpenBehavior(Type openBehaviorType)
    {
        if (!openBehaviorType.IsGenericTypeDefinition)
            throw new ArgumentException("O behavior deve ser um tipo genérico aberto.", nameof(openBehaviorType));

        OpenBehaviors.Add(openBehaviorType);
        return this;
    }
}

public static class DependencyInjection
{
    /// <remarks>
    /// Os métodos <c>TryAdd*</c> tornam o registro idempotente: chamar <c>AddMediator</c> mais de uma vez
    /// ou informar o mesmo assembly duas vezes não duplica handlers nem behaviors.
    /// </remarks>
    public static IServiceCollection AddMediator(this IServiceCollection services, Action<MediatorConfiguration> configure)
    {
        var configuration = new MediatorConfiguration();
        configure(configuration);

        services.TryAddScoped<IMediator, Mediator>();
        services.TryAddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());
        services.TryAddScoped<IPublisher>(sp => sp.GetRequiredService<IMediator>());

        var implementations = configuration.Assemblies
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });

        foreach (var implementation in implementations)
        {
            foreach (var @interface in implementation.GetInterfaces().Where(i => i.IsGenericType))
            {
                var definition = @interface.GetGenericTypeDefinition();

                // Requisição: exatamente um handler por tipo. Dois handlers diferentes = erro de configuração.
                if (definition == typeof(IRequestHandler<,>))
                {
                    var existente = services.FirstOrDefault(d => d.ServiceType == @interface);
                    if (existente is null)
                        services.AddScoped(@interface, implementation);
                    else if (existente.ImplementationType != implementation)
                        throw new InvalidOperationException(
                            $"Mais de um handler para '{@interface.GenericTypeArguments[0].Name}': " +
                            $"'{existente.ImplementationType?.Name}' e '{implementation.Name}'.");
                }

                // Notificação: vários handlers por tipo, mas sem repetir a mesma implementação.
                else if (definition == typeof(INotificationHandler<>))
                    services.TryAddEnumerable(ServiceDescriptor.Scoped(@interface, implementation));
            }
        }

        foreach (var behavior in configuration.OpenBehaviors)
            services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>), behavior));

        return services;
    }
}
