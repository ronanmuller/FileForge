using FileForge.Application.Solicitacoes.Concluir;
using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;
using FileForge.Infrastructure.Outbox;
using FileForge.Infrastructure.Persistence;
using FileForge.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.IntegrationTests.Fluxo;

/// <summary>
/// Prova que a conclusão grava <c>ArquivoGerado</c> no Outbox na mesma transação do status,
/// e que, se a conclusão for recusada, nada entra no Outbox.
/// </summary>
public sealed class ConcluirSolicitacaoOutboxTests() : PostgresFixture("fileforge_test_outbox")
{
    [Fact]
    public async Task Concluir_GravaArquivoGeradoNoOutboxNaMesmaTransacao()
    {
        var id = await CriarEmProcessamento();

        await using (var escopo = Escopo())
        {
            var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();
            var resultado = await mediator.Send(new ConcluirSolicitacaoCommand(id, "extrato.txt"));
            Assert.True(resultado.IsSuccess);
        }

        await using var leitura = Escopo();
        var db = leitura.ServiceProvider.GetRequiredService<FileForgeDbContext>();

        var status = await db.SolicitacoesArquivo.AsNoTracking()
            .Where(s => s.Id == new SolicitacaoArquivoId(id))
            .Select(s => s.Status)
            .SingleAsync();
        Assert.Equal(StatusSolicitacao.Concluida, status);

        var mensagem = Assert.Single(await db.OutboxMessages.AsNoTracking().ToListAsync());
        Assert.Equal(nameof(FileForge.Domain.Events.ArquivoGerado), mensagem.Tipo);
        Assert.True(mensagem.Pendente);
    }

    [Fact]
    public async Task Concluir_RecusadaPorTransicaoInvalida_NaoGravaNadaNoOutbox()
    {
        var id = await CriarPendente();

        await using (var escopo = Escopo())
        {
            var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();
            var resultado = await mediator.Send(new ConcluirSolicitacaoCommand(id, "extrato.txt"));
            Assert.True(resultado.IsFailure);
        }

        await using var leitura = Escopo();
        var db = leitura.ServiceProvider.GetRequiredService<FileForgeDbContext>();
        Assert.Empty(await db.OutboxMessages.AsNoTracking().ToListAsync());
    }

    private async Task<Guid> CriarPendente()
    {
        var solicitacao = SolicitacaoArquivo.Criar(
            ClienteId.Novo(),
            DataReferencia.Criar(new DateOnly(2025, 1, 10), Relogio).Value,
            "ana",
            Relogio);

        await Salvar(db => db.SolicitacoesArquivo.Add(solicitacao));
        return solicitacao.Id.Value;
    }

    private async Task<Guid> CriarEmProcessamento()
    {
        var id = await CriarPendente();

        await using var escopo = Escopo();
        var db = escopo.ServiceProvider.GetRequiredService<FileForgeDbContext>();
        var solicitacao = await db.SolicitacoesArquivo.SingleAsync(s => s.Id == new SolicitacaoArquivoId(id));
        solicitacao.IniciarProcessamento(Relogio);
        await db.SaveChangesAsync();

        return id;
    }
}
