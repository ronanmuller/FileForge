using FileForge.Application;
using FileForge.Application.Abstractions.Seguranca;
using FileForge.Domain.Events;
using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;
using FileForge.Infrastructure;
using FileForge.Infrastructure.Persistence;
using FileForge.Mediator;
using FileForge.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AppCriarSolicitacaoCommand = FileForge.Application.Solicitacoes.Criar.CriarSolicitacaoCommand;

namespace FileForge.IntegrationTests.Fluxo;

/// <summary>
/// Prova que a criação da solicitação grava <c>SolicitacaoArquivoCriada</c> no Outbox
/// na mesma transação do pedido, e que falhas de negócio não geram mensagens.
/// </summary>
public sealed class CriarSolicitacaoOutboxTests() : PostgresFixture("fileforge_test_criar_outbox")
{
    [Fact]
    public async Task Criar_GravaSolicitacaoEEventoNoOutboxNaMesmaTransacao()
    {
        var clienteId = ClienteId.Novo();
        var data = new DateOnly(2025, 1, 10);

        await using var provedor = CriarProvedorComoCliente(clienteId);
        await using (var escopo = provedor.CreateAsyncScope())
        {
            var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();
            var resultado = await mediator.Send(new AppCriarSolicitacaoCommand(data));
            Assert.True(resultado.IsSuccess);
        }

        await using var leitura = Escopo();
        var db = leitura.ServiceProvider.GetRequiredService<FileForgeDbContext>();

        var solicitacao = await db.SolicitacoesArquivo.AsNoTracking().SingleAsync();
        Assert.Equal(clienteId, solicitacao.ClienteId);
        Assert.Equal(StatusSolicitacao.Pendente, solicitacao.Status);

        var mensagem = Assert.Single(await db.OutboxMessages.AsNoTracking().ToListAsync());
        Assert.Equal(nameof(SolicitacaoArquivoCriada), mensagem.Tipo);
        Assert.True(mensagem.Pendente);
    }

    [Fact]
    public async Task Criar_DuplicadaAtiva_NaoGravaSegundaMensagemNoOutbox()
    {
        var clienteId = ClienteId.Novo();
        var data = new DateOnly(2025, 1, 10);

        await using var provedor = CriarProvedorComoCliente(clienteId);
        await using (var escopo = provedor.CreateAsyncScope())
        {
            var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();
            var primeira = await mediator.Send(new AppCriarSolicitacaoCommand(data));
            var segunda = await mediator.Send(new AppCriarSolicitacaoCommand(data));

            Assert.True(primeira.IsSuccess);
            Assert.True(segunda.IsFailure);
        }

        await using var leitura = Escopo();
        var db = leitura.ServiceProvider.GetRequiredService<FileForgeDbContext>();
        Assert.Single(await db.OutboxMessages.AsNoTracking().ToListAsync());
    }

    private ServiceProvider CriarProvedorComoCliente(ClienteId clienteId) =>
        new ServiceCollection()
            .AddInfrastructure(ConexaoAtual)
            .AddApplication()
            .AddLogging()
            .AddSingleton<IDateTimeProvider>(Relogio)
            .AddSingleton<IUsuarioAtual>(new UsuarioClienteTeste(clienteId))
            .BuildServiceProvider();

    private sealed class UsuarioClienteTeste(ClienteId clienteId) : IUsuarioAtual
    {
        public bool Autenticado => true;
        public string Id => "ana";
        public ClienteId? ClienteId => clienteId;
        public bool PossuiPermissao(string permissao) => true;
    }
}
