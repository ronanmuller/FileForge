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
    public async Task Publish_QuandoUmHandlerFalha_DeveExecutarOsDemaisEPropagarAFalha()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExecutionLog>();
        services.AddMediator(cfg => cfg.RegisterHandlersFromAssembly(typeof(MediatorCorrecoesTests).Assembly));
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var log = provider.GetRequiredService<ExecutionLog>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Publish(new EventoComFalha()));

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

    [Fact]
    public async Task Publish_UmaUnicaFalha_LancaAExcecaoOriginal()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExecutionLog>();
        services.AddMediator(cfg => cfg.RegisterHandlersFromAssembly(typeof(MediatorCorrecoesTests).Assembly));
        using var provider = services.BuildServiceProvider();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetRequiredService<IMediator>().Publish(new EventoComFalha()));

        Assert.Equal("falhou", ex.Message);
    }

    [Fact]
    public async Task Publish_Cancelado_PropagaCancelamentoSemAgregar()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExecutionLog>();
        services.AddMediator(cfg => cfg.RegisterHandlersFromAssembly(typeof(MediatorCorrecoesTests).Assembly));
        using var provider = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetRequiredService<IMediator>().Publish(new AlgoAconteceu("x"), cts.Token));

        Assert.Empty(provider.GetRequiredService<ExecutionLog>().Entries);
    }

    [Fact]
    public void AddMediator_DoisHandlersParaOMesmoRequest_LancaErroDeConfiguracao()
    {
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<Ping, string>, OutroPingHandler<int>>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddMediator(cfg => cfg.RegisterHandlersFromAssembly(typeof(MediatorCorrecoesTests).Assembly)));

        Assert.Contains(nameof(Ping), ex.Message);
    }

    // Genérico aberto: o scanner ignora definições genéricas, então só existe quando registrado manualmente.
    private sealed class OutroPingHandler<T> : IRequestHandler<Ping, string>
    {
        public Task<string> Handle(Ping request, CancellationToken cancellationToken) => Task.FromResult("outro");
    }
}
