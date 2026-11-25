using System.Diagnostics;
using FileForge.Domain.Interfaces;
using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;
using FileForge.Infrastructure;
using FileForge.Infrastructure.Persistence;
using FileForge.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;

namespace FileForge.IntegrationTests.Carga;

/// <summary>
/// Teste de carga de <c>ListarPorContasEDataAsync</c>. Fica fora do build normal:
/// <c>dotnet test --filter "Categoria=Carga"</c>. Mede o tempo e imprime o plano de execução do PostgreSQL.
/// </summary>
[Trait("Categoria", "Carga")]
public sealed class TransacaoCargaTests(ITestOutputHelper saida) : IAsyncLifetime
{
    private static readonly string Conn =
        Environment.GetEnvironmentVariable("FILEFORGE_CARGA_CONN")
        ?? "Host=localhost;Port=5432;Database=fileforge_carga;Username=fileforge;Password=fileforge";

    private const int _quantidadeDeContas = 10;
    private const int _transacoesPorConta = 100_000;
    private static readonly DateOnly DiaAlvo = new(2025, 1, 10);

    private readonly RelogioCarga _relogio = new();
    private ServiceProvider _provider = null!;
    private List<ContaId> _contas = [];
    private int _esperadasNoDia;

    public async Task InitializeAsync()
    {
        _provider = new ServiceCollection().AddInfrastructure(Conn).BuildServiceProvider();
        var db = _provider.GetRequiredService<FileForgeDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await PopularAsync(db);
    }

    public async Task DisposeAsync()
    {
        var db = _provider.GetRequiredService<FileForgeDbContext>();
        await db.Database.EnsureDeletedAsync();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task ListarPorContasEData_VolumeGrande_EntregaExatamenteAsEsperadas()
    {
        var data = DataReferencia.Criar(DiaAlvo, _relogio).Value;

        await using var escopo = _provider.CreateAsyncScope();
        var repo = escopo.ServiceProvider.GetRequiredService<ITransacaoRepository>();

        var sw = Stopwatch.StartNew();
        var total = 0;
        await foreach (var _ in repo.ListarPorContasEDataAsync(_contas, data, default))
            total++;
        sw.Stop();

        saida.WriteLine($"Contas: {_contas.Count} | Transações lidas: {total} | Tempo: {sw.ElapsedMilliseconds} ms");
        Assert.Equal(_esperadasNoDia, total);
    }

    [Fact]
    public async Task ListarPorContasEData_ImprimePlanoDeExecucao()
    {
        var ids = _contas.Select(c => c.Value).ToArray();

        const string sql = """
            EXPLAIN (ANALYZE, BUFFERS)
            SELECT t.* FROM transacoes t
            WHERE t.conta_id = ANY(@ids) AND t.data = @data
            ORDER BY t.data, t.id
            """;

        await using var conexao = new NpgsqlConnection(Conn);
        await conexao.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conexao);
        cmd.Parameters.AddWithValue("ids", ids);
        cmd.Parameters.AddWithValue("data", DiaAlvo);

        await using var leitor = await cmd.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
            saida.WriteLine(leitor.GetString(0));
    }

    [Fact]
    public async Task ListarPorContasEData_CancelamentoInterrompeALeitura()
    {
        var data = DataReferencia.Criar(DiaAlvo, _relogio).Value;
        using var cts = new CancellationTokenSource();

        await using var escopo = _provider.CreateAsyncScope();
        var repo = escopo.ServiceProvider.GetRequiredService<ITransacaoRepository>();

        var lidas = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in repo.ListarPorContasEDataAsync(_contas, data, cts.Token))
            {
                if (++lidas == 1000)
                    await cts.CancelAsync();
            }
        });

        Assert.True(lidas < _esperadasNoDia);
    }

    private async Task PopularAsync(FileForgeDbContext db)
    {
        var cliente = Cliente.Criar("Carga", Documento.Criar("52998224725").Value).Value;
        for (var i = 0; i < _quantidadeDeContas; i++)
            cliente.AdicionarConta($"{i:D5}", $"{i:D12}");

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        _contas = cliente.Contas.Select(c => c.Id).ToList();
        var valor = Dinheiro.Criar(10m).Value;

        foreach (var conta in _contas)
        {
            var lote = new List<Transacao>(_transacoesPorConta);

            for (var i = 0; i < _transacoesPorConta; i++)
            {
                var dia = DiaAlvo.AddDays(i % 30);
                lote.Add(Transacao.Registrar(conta, dia, valor,
                    i % 2 == 0 ? TipoTransacao.Credito : TipoTransacao.Debito, $"carga {i}").Value);

                if (dia == DiaAlvo)
                    _esperadasNoDia++;
            }

            db.Transacoes.AddRange(lote);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }

    private sealed class RelogioCarga : IDateTimeProvider
    {
        public DateTime UtcNow => new(2025, 2, 1, 12, 0, 0, DateTimeKind.Utc);
        public DateOnly Hoje => DateOnly.FromDateTime(UtcNow);
    }
}
