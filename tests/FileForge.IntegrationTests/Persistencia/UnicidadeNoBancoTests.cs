using FileForge.Application.Abstractions.Persistencia;
using FileForge.Domain.Interfaces;
using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;
using FileForge.Infrastructure;
using FileForge.Infrastructure.Persistence;
using FileForge.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.IntegrationTests.Persistencia;

/// <summary>
/// Prova a janela de corrida: duas criações passam pela checagem <c>ExisteAtivaAsync</c> antes de qualquer commit.
/// Só o índice único parcial pode barrar a segunda, e a Infrastructure precisa traduzir isso em
/// <see cref="UnicidadeVioladaException"/> com o nome da constraint.
/// </summary>
public sealed class UnicidadeNoBancoTests : IAsyncLifetime
{
    private static readonly string Conn =
        Environment.GetEnvironmentVariable("FILEFORGE_TEST_CONN")
        ?? "Host=localhost;Port=5432;Database=fileforge_test_unicidade;Username=fileforge;Password=fileforge";

    private readonly RelogioTeste _relogio = new();
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        _provider = new ServiceCollection().AddInfrastructure(Conn).BuildServiceProvider();
        var db = _provider.GetRequiredService<FileForgeDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        var db = _provider.GetRequiredService<FileForgeDbContext>();
        await db.Database.EnsureDeletedAsync();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task SegundaSolicitacaoAtiva_MesmaDataEClienteNoBanco_LancaUnicidadeComNomeDaRestricao()
    {
        var clienteId = ClienteId.Novo();
        var data = DataReferencia.Criar(new DateOnly(2025, 1, 10), _relogio).Value;

        // Ambas passam pela checagem antes do commit: a corrida real.
        await using (var primeiro = _provider.CreateAsyncScope())
        await using (var segundo = _provider.CreateAsyncScope())
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

            Assert.Equal(RestricoesDeBanco.SolicitacaoAtiva, ex.Restricao);
        }
    }

    private sealed class RelogioTeste : IDateTimeProvider
    {
        public DateTime UtcNow => new(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        public DateOnly Hoje => DateOnly.FromDateTime(UtcNow);
    }
}
