using FileForge.Application.Solicitacoes.Concluir;
using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;
using FileForge.Infrastructure.Outbox;
using FileForge.Infrastructure.Persistence;
using FileForge.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FileForge.SharedKernel.Abstractions;

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

    [Fact]
    public async Task Concluir_FalhaNoCommitAposAsDuasEscritas_NemStatusNemOutboxSaoGravados()
    {
        var id = await CriarEmProcessamento();

        await using (var escopo = Escopo())
        {
            var db = escopo.ServiceProvider.GetRequiredService<FileForgeDbContext>();
            var registrador = escopo.ServiceProvider.GetRequiredService<IRegistradorDeEventos>();
            var unitOfWork = escopo.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var solicitacao = await db.SolicitacoesArquivo.SingleAsync(s => s.Id == new SolicitacaoArquivoId(id));
            var resultado = solicitacao.Concluir("extrato.txt", Relogio);
            Assert.True(resultado.IsSuccess);

            // 1ª escrita: ArquivoGerado válido, pelo caminho real do registrador.
            registrador.RegistrarEventos();

            // 2ª escrita: Tipo com 201 caracteres numa coluna varchar(200). O PostgreSQL recusa
            // depois que o status e a primeira mensagem já estão no mesmo SaveChanges.
            db.OutboxMessages.Add(new OutboxMessage(Guid.NewGuid(), new string('x', 201), "{}", Relogio.UtcNow));

            await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.SaveChangesAsync());
        }

        await using var leitura = Escopo();
        var banco = leitura.ServiceProvider.GetRequiredService<FileForgeDbContext>();

        var status = await banco.SolicitacoesArquivo.AsNoTracking()
            .Where(s => s.Id == new SolicitacaoArquivoId(id))
            .Select(s => s.Status)
            .SingleAsync();
        Assert.Equal(StatusSolicitacao.Processando, status);

        Assert.Empty(await banco.OutboxMessages.AsNoTracking().ToListAsync());
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
