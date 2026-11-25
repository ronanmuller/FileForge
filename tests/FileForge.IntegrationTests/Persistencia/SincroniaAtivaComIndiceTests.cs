using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;
using FileForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.IntegrationTests.Persistencia;

/// <summary>
/// Garante que "ativa" é a mesma regra no domínio (<see cref="SolicitacaoArquivo.Ativa"/>) e no índice
/// parcial do banco. Se um lado mudar sem o outro, este teste falha.
/// </summary>
public sealed class SincroniaAtivaComIndiceTests() : PostgresFixture("fileforge_test_sincronia")
{
    [Fact]
    public async Task StatusAtivos_BatemComOFiltroDoIndiceNoBanco()
    {
        await using var escopo = Escopo();
        var db = escopo.ServiceProvider.GetRequiredService<FileForgeDbContext>();

        var filtro = await db.Database
            .SqlQueryRaw<string>("SELECT pg_get_expr(indpred, indrelid) AS \"Value\" FROM pg_index WHERE indexrelid = 'ux_solicitacao_ativa'::regclass")
            .SingleAsync();

        foreach (var status in Enum.GetValues<StatusSolicitacao>())
        {
            var esperadoNoFiltro = SolicitacaoArquivo.StatusAtivos.Contains(status);
            Assert.Equal(esperadoNoFiltro, filtro.Contains($"'{status}'"));
        }
    }

    [Fact]
    public void AtivaDoAggregate_CoincideComAListaUsadaNoIndice()
    {
        foreach (var status in Enum.GetValues<StatusSolicitacao>())
        {
            var solicitacao = Reconstituir(status);
            Assert.Equal(SolicitacaoArquivo.StatusAtivos.Contains(status), solicitacao.Ativa);
        }
    }

    private static SolicitacaoArquivo Reconstituir(StatusSolicitacao status)
    {
        var relogio = new RelogioTeste();
        var data = DataReferencia.Criar(new DateOnly(2025, 1, 10), relogio).Value;
        var solicitacao = SolicitacaoArquivo.Criar(ClienteId.Novo(), data, "ana", relogio);

        if (status is StatusSolicitacao.Processando or StatusSolicitacao.Concluida or StatusSolicitacao.Falhou)
            solicitacao.IniciarProcessamento(relogio);
        if (status is StatusSolicitacao.Concluida)
            solicitacao.Concluir("extrato.txt", relogio);
        if (status is StatusSolicitacao.Falhou)
            solicitacao.Falhar("erro", relogio);

        return solicitacao;
    }
}
