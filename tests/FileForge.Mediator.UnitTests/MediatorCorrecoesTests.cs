using Microsoft.Extensions.DependencyInjection;

namespace FileForge.Mediator.UnitTests;

public sealed record EventoComFalha : INotification;

public sealed class HandlerQueFalha : INotificationHandler<EventoComFalha>
{
    public Task Handle(EventoComFalha notification, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("falhou");
}

public sealed class HandlerQueRegistra(ExecutionLog log) : INotificationHandler<EventoComFalha>
{
    public Task Handle(EventoComFalha notification, CancellationToken cancellationToken)
    {
        log.Entries.Add("executou");
        return Task.CompletedTask;
    }
}

public class MediatorCorrecoesTests
{
    [Fact]
    public async Task Publish_QuandoUmHandlerFalha_DeveExecutarOsDemaisELancarAggregateException()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExecutionLog>();
        services.AddMediator(cfg => cfg.RegisterHandlersFromAssembly(typeof(MediatorCorrecoesTests).Assembly));
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var log = provider.GetRequiredService<ExecutionLog>();

        var exception = await Assert.ThrowsAsync<AggregateException>(() => mediator.Publish(new EventoComFalha()));

        Assert.Single(exception.InnerExceptions);
        Assert.Contains("executou", log.Entries);
    }

    [Fact]
    public async Task AddMediator_ChamadoDuasVezes_NaoDeveDuplicarHandlers()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExecutionLog>();
        var assembly = typeof(MediatorCorrecoesTests).Assembly;
        services.AddMediator(cfg => cfg.RegisterHandlersFromAssembly(assembly).RegisterHandlersFromAssembly(assembly));
        services.AddMediator(cfg => cfg.RegisterHandlersFromAssembly(assembly));
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var log = provider.GetRequiredService<ExecutionLog>();

        await mediator.Publish(new AlgoAconteceu("v"));

        Assert.Equal(2, log.Entries.Count);
    }
}
