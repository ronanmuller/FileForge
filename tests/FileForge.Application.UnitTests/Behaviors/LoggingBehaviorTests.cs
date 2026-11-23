using FileForge.Application.Behaviors;
using FileForge.Mediator;
using FileForge.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace FileForge.Application.UnitTests.Behaviors;

public class LoggingBehaviorTests
{
    private sealed record Comando : IRequest<Result>;

    /// <summary>Logger em memória: captura nível e EventId para verificar o que foi logado.</summary>
    private sealed class LoggerFake<T> : ILogger<T>
    {
        public List<(LogLevel Nivel, int EventId, string Mensagem, Exception? Erro)> Entradas { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entradas.Add((logLevel, eventId.Id, formatter(state, exception), exception));
    }

    private readonly LoggerFake<LoggingBehavior<Comando, Result>> _logger = new();
    private LoggingBehavior<Comando, Result> Behavior => new(_logger);

    [Fact]
    public async Task Sucesso_LogaInicioEConclusao()
    {
        await Behavior.Handle(new Comando(), () => Task.FromResult(Result.Success()), CancellationToken.None);

        Assert.Equal([1000, 1001], _logger.Entradas.Select(e => e.EventId));
        Assert.All(_logger.Entradas, e => Assert.Equal(LogLevel.Information, e.Nivel));
    }

    [Fact]
    public async Task FalhaDeNegocio_LogaWarningComCodigoDoErro()
    {
        var erro = Error.Conflict("Teste.Conflito", "já existe");

        await Behavior.Handle(new Comando(), () => Task.FromResult(Result.Failure(erro)), CancellationToken.None);

        var falha = Assert.Single(_logger.Entradas, e => e.EventId == 1002);
        Assert.Equal(LogLevel.Warning, falha.Nivel);
        Assert.Contains("Teste.Conflito", falha.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Excecao_LogaErrorERelanca()
    {
        var ex = new InvalidOperationException("bug");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Behavior.Handle(new Comando(), () => throw ex, CancellationToken.None));

        var erro = Assert.Single(_logger.Entradas, e => e.EventId == 1003);
        Assert.Equal(LogLevel.Error, erro.Nivel);
        Assert.Same(ex, erro.Erro);
    }

    [Fact]
    public async Task Cancelamento_NaoEhLogadoComoErro()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Behavior.Handle(new Comando(), () => throw new OperationCanceledException(), CancellationToken.None));

        Assert.DoesNotContain(_logger.Entradas, e => e.Nivel == LogLevel.Error);
    }

    [Fact]
    public async Task RequisicaoLenta_LogaWarningDeLentidao()
    {
        await Behavior.Handle(new Comando(), async () =>
        {
            await Task.Delay(LoggingBehavior<Comando, Result>.LimiteLentidaoMs + 50);
            return Result.Success();
        }, CancellationToken.None);

        Assert.Contains(_logger.Entradas, e => e.EventId == 1004 && e.Nivel == LogLevel.Warning);
    }
}
