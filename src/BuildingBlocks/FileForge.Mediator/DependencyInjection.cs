using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

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
    private static readonly Type[] HandlerInterfaces =
    [
        typeof(IRequestHandler<,>),
        typeof(INotificationHandler<>)
    ];

    public static IServiceCollection AddMediator(this IServiceCollection services, Action<MediatorConfiguration> configure)
    {
        var configuration = new MediatorConfiguration();
        configure(configuration);

        services.AddScoped<IMediator, Mediator>();
        services.AddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());
        services.AddScoped<IPublisher>(sp => sp.GetRequiredService<IMediator>());

        var handlerTypes = configuration.Assemblies
            .Distinct()
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });

        foreach (var implementation in handlerTypes)
        {
            var interfaces = implementation.GetInterfaces()
                .Where(i => i.IsGenericType && HandlerInterfaces.Contains(i.GetGenericTypeDefinition()));

            foreach (var @interface in interfaces)
                services.AddScoped(@interface, implementation);
        }

        foreach (var behavior in configuration.OpenBehaviors)
            services.AddScoped(typeof(IPipelineBehavior<,>), behavior);

        return services;
    }
}
