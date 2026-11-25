using FileForge.Domain.Interfaces;
using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;
using FileForge.Infrastructure.Persistence;
using FileForge.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.IntegrationTests.Persistencia;

/// <summary>
/// Prova a janela de corrida: duas criações passam pela checagem <c>ExisteAtivaAsync</c> antes de qualquer commit.
/// Só o índice único parcial pode barrar a segunda, e a Infrastructure precisa traduzir isso em
/// <see cref="UnicidadeVioladaException"/> com o nome da constraint.
/// </summary>
public sealed class UnicidadeNoBancoTests() : PostgresFixture("fileforge_test_unicidade")
{
    private RelogioTeste _relogio => Relogio;

    [Fact]
    public async Task SegundaSolicitacaoAtiva_MesmaDataEClienteNoBanco_LancaUnicidadeComNomeDaRestricao()
    {
        var clienteId = ClienteId.Novo();
        var data = DataReferencia.Criar(new DateOnly(2025, 1, 10), _relogio).Value;

        // Ambas passam pela checagem antes do commit: a corrida real.
        await using (var primeiro = Provider.CreateAsyncScope())
        await using (var segundo = Provider.CreateAsyncScope())
        {
            var repo1 = primeiro.ServiceProvider.GetRequiredService<ISolicitacaoArquivoRepository>();
            var repo2 = segundo.ServiceProvider.GetRequiredService<ISolicitacaoArquivoRepository>();
            var uow1 = primeiro.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var uow2 = segundo.ServiceProvider.GetRequiredService<IUnitOfWork>();

            Assert.False(await repo1.ExisteAtivaAsync(clienteId, data, default));
            Assert.False(await repo2.ExisteAtivaAsync(clienteId, data, default));

            repo1.Adicionar(SolicitacaoArquivo.Criar(clienteId, data, "ana", _relogio));
            await uow1.SaveChangesAsync();

            repo2.Adicionar(SolicitacaoArquivo.Criar(clienteId, data, "ana", _relogio));
            var ex = await Assert.ThrowsAsync<UnicidadeVioladaException>(() => uow2.SaveChangesAsync());

            Assert.Equal(RestricaoUnica.SolicitacaoAtiva, ex.Restricao);
        }
    }
}
