using Microsoft.Extensions.DependencyInjection;

namespace FileForge.Mediator.UnitTests;

public sealed record Ping(string Message) : IRequest<string>;

public sealed class PingHandler : IRequestHandler<Ping, string>
{
    public Task<string> Handle(Ping request, CancellationToken cancellationToken) =>
        Task.FromResult($"Pong: {request.Message}");
}

public sealed record SemHandler : IRequest<int>;

public sealed record AlgoAconteceu(string Valor) : INotification;

public sealed class ExecutionLog
{
    public List<string> Entries { get; } = [];
}

public sealed class PrimeiroHandler(ExecutionLog log) : INotificationHandler<AlgoAconteceu>
{
    public Task Handle(AlgoAconteceu notification, CancellationToken cancellationToken)
    {
        log.Entries.Add($"primeiro:{notification.Valor}");
        return Task.CompletedTask;
    }
}

public sealed class SegundoHandler(ExecutionLog log) : INotificationHandler<AlgoAconteceu>
{
    public Task Handle(AlgoAconteceu notification, CancellationToken cancellationToken)
    {
        log.Entries.Add($"segundo:{notification.Valor}");
        return Task.CompletedTask;
    }
}

public sealed class OuterBehavior<TRequest, TResponse>(ExecutionLog log) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        log.Entries.Add("outer:antes");
        var response = await next();
        log.Entries.Add("outer:depois");
        return response;
    }
}

public sealed class InnerBehavior<TRequest, TResponse>(ExecutionLog log) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        log.Entries.Add("inner:antes");
        var response = await next();
        log.Entries.Add("inner:depois");
        return response;
    }
}

public class MediatorTests
{
    private static ServiceProvider BuildProvider(Action<MediatorConfiguration>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExecutionLog>();
        services.AddMediator(cfg =>
        {
            cfg.RegisterHandlersFromAssembly(typeof(MediatorTests).Assembly);
            extra?.Invoke(cfg);
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Send_DeveInvocarHandlerERetornarResposta()
    {
        using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var response = await mediator.Send(new Ping("oi"));

        Assert.Equal("Pong: oi", response);
    }

    [Fact]
    public async Task Send_SemHandlerRegistrado_DeveLancarInvalidOperationException()
    {
        using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Send(new SemHandler()));
    }

    [Fact]
    public async Task Send_RequestNulo_DeveLancarArgumentNullException()
    {
        using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Send<string>(null!));
    }

    [Fact]
    public async Task Send_DeveExecutarBehaviorsNaOrdemDeRegistro()
    {
        using var provider = BuildProvider(cfg => cfg
            .AddOpenBehavior(typeof(OuterBehavior<,>))
            .AddOpenBehavior(typeof(InnerBehavior<,>)));
        var mediator = provider.GetRequiredService<IMediator>();
        var log = provider.GetRequiredService<ExecutionLog>();

        await mediator.Send(new Ping("x"));

        Assert.Equal(["outer:antes", "inner:antes", "inner:depois", "outer:depois"], log.Entries);
    }

    [Fact]
    public async Task Publish_DeveInvocarTodosOsHandlers()
    {
        using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var log = provider.GetRequiredService<ExecutionLog>();

        await mediator.Publish(new AlgoAconteceu("v"));

        Assert.Equal(2, log.Entries.Count);
        Assert.Contains("primeiro:v", log.Entries);
        Assert.Contains("segundo:v", log.Entries);
    }

    [Fact]
    public async Task Publish_ComoINotification_DeveUsarTipoEmTempoDeExecucao()
    {
        using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var log = provider.GetRequiredService<ExecutionLog>();

        INotification notification = new AlgoAconteceu("dinamico");
        await mediator.Publish(notification);

        Assert.Equal(2, log.Entries.Count);
    }

    [Fact]
    public void AddOpenBehavior_ComTipoFechado_DeveLancarArgumentException()
    {
        var configuration = new MediatorConfiguration();

        Assert.Throws<ArgumentException>(() =>
            configuration.AddOpenBehavior(typeof(OuterBehavior<Ping, string>)));
    }
}
