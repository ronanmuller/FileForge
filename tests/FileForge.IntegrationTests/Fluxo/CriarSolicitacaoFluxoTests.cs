using FileForge.Domain.Erros;
using FileForge.Domain.Solicitacoes;
using FileForge.Mediator;
using FileForge.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.IntegrationTests.Fluxo;

/// <summary>
/// Integração real entre DI + Mediator + Pipeline + Domain (sem banco ainda).
/// Rode em Debug com breakpoint no handler e no behavior para ver o fluxo completo.
/// </summary>
public sealed class CriarSolicitacaoFluxoTests : IDisposable
{
    private sealed class RelogioFixo : IDateTimeProvider
    {
        public DateTime UtcNow => new(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        public DateOnly Hoje => new(2025, 1, 15);
    }

    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;

    public CriarSolicitacaoFluxoTests()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IDateTimeProvider, RelogioFixo>();
        services.AddScoped<RepositorioSolicitacoesEmMemoria>();
        services.AddScoped<Rastro>();
        services.AddMediator(cfg => cfg
            .RegisterHandlersFromAssembly(typeof(CriarSolicitacaoFluxoTests).Assembly)
            .AddOpenBehavior(typeof(RastreamentoBehavior<,>)));

        _root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _scope = _root.CreateScope();
    }

    private T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    [Fact]
    public async Task Send_ComandoValido_PassaPeloBehaviorECriaSolicitacao()
    {
        var mediator = Get<IMediator>();

        var result = await mediator.Send(new CriarSolicitacaoCommand(Guid.NewGuid(), new DateOnly(2025, 1, 10)));

        Assert.True(result.IsSuccess);
        var salva = Assert.Single(Get<RepositorioSolicitacoesEmMemoria>().Itens);
        Assert.Equal(result.Value, salva.Id.Value);
        Assert.Equal(StatusSolicitacao.Pendente, salva.Status);
        Assert.Equal(
            ["antes:CriarSolicitacaoCommand", "depois:CriarSolicitacaoCommand"],
            Get<Rastro>().Passos);
    }

    [Fact]
    public async Task Send_MesmoClienteEData_SegundaVezRetornaConflito()
    {
        var mediator = Get<IMediator>();
        var command = new CriarSolicitacaoCommand(Guid.NewGuid(), new DateOnly(2025, 1, 10));

        await mediator.Send(command);
        var segunda = await mediator.Send(command);

        Assert.Equal(DomainErrors.SolicitacaoArquivo.JaExisteAtiva, segunda.Error);
        Assert.Single(Get<RepositorioSolicitacoesEmMemoria>().Itens);
    }

    [Fact]
    public async Task Send_AposFalhaDaAnterior_PermiteNovaSolicitacao()
    {
        var mediator = Get<IMediator>();
        var relogio = Get<IDateTimeProvider>();
        var command = new CriarSolicitacaoCommand(Guid.NewGuid(), new DateOnly(2025, 1, 10));

        await mediator.Send(command);
        var primeira = Get<RepositorioSolicitacoesEmMemoria>().Itens[0];
        primeira.IniciarProcessamento(relogio);
        primeira.Falhar("bucket indisponível", relogio); // deixa de ser "ativa"

        var segunda = await mediator.Send(command);

        Assert.True(segunda.IsSuccess);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", 2025, 1, 10, "Cliente.IdVazio")]
    [InlineData("8a1f6c2e-0000-0000-0000-000000000001", 2025, 1, 16, "DataReferencia.NoFuturo")]
    public async Task Send_EntradaInvalida_RetornaErroDeDominioSemExcecao(string clienteId, int a, int m, int d, string codigo)
    {
        var result = await Get<IMediator>().Send(new CriarSolicitacaoCommand(Guid.Parse(clienteId), new DateOnly(a, m, d)));

        Assert.True(result.IsFailure);
        Assert.Equal(codigo, result.Error.Code);
        Assert.Empty(Get<RepositorioSolicitacoesEmMemoria>().Itens);
    }

    [Fact]
    public async Task Publish_EventosDoAggregate_ChegamAosHandlersViaPonte()
    {
        var mediator = Get<IMediator>();
        var relogio = Get<IDateTimeProvider>();
        await mediator.Send(new CriarSolicitacaoCommand(Guid.NewGuid(), new DateOnly(2025, 1, 10)));
        var solicitacao = Get<RepositorioSolicitacoesEmMemoria>().Itens[0];
        solicitacao.IniciarProcessamento(relogio);
        solicitacao.Concluir("extrato.txt", relogio);

        // Simula o que a infraestrutura (Outbox) fará após o commit.
        foreach (var evento in solicitacao.DomainEvents)
            await mediator.Publish(new DomainEventNotification(evento));
        solicitacao.ClearDomainEvents();

        Assert.Equal(
            ["evento:SolicitacaoArquivoCriada", "evento:ProcessamentoIniciado", "evento:ArquivoGerado"],
            Get<Rastro>().Passos.Where(p => p.StartsWith("evento:")));
    }

    public void Dispose()
    {
        _scope.Dispose();
        _root.Dispose();
    }
}
