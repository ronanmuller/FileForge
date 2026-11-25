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
/// Repositórios contra um PostgreSQL real. Usa FILEFORGE_TEST_CONN ou o container local (porta 5432).
/// O banco é recriado do zero para cada teste, então um teste não enxerga dados de outro.
/// </summary>
public sealed class RepositoriosPostgresTests : IAsyncLifetime
{
    private static readonly string Conn =
        Environment.GetEnvironmentVariable("FILEFORGE_TEST_CONN")
        ?? "Host=localhost;Port=5432;Database=fileforge_test_repositorios;Username=fileforge;Password=fileforge";

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

    private AsyncServiceScope Escopo() => _provider.CreateAsyncScope();

    private async Task Salvar(Action<FileForgeDbContext> adicionar)
    {
        await using var escopo = Escopo();
        var db = escopo.ServiceProvider.GetRequiredService<FileForgeDbContext>();
        adicionar(db);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Cliente_ComContas_PersisteELeEmTransacao()
    {
        var cliente = Cliente.Criar("Ana", Documento.Criar("52998224725").Value).Value;
        cliente.AdicionarConta("00001", "123456789012");
        await Salvar(db => db.Clientes.Add(cliente));

        await using var leitura = Escopo();
        var lido = await leitura.ServiceProvider.GetRequiredService<FileForgeDbContext>()
            .Clientes.Include(c => c.Contas).SingleAsync(c => c.Id == cliente.Id);

        Assert.Equal("Ana", lido.Nome);
        Assert.Equal("52998224725", lido.Documento.Numero);
        Assert.Single(lido.Contas);
    }

    [Fact]
    public async Task ObterPorId_CarregaAsContas()
    {
        var cliente = Cliente.Criar("Ana", Documento.Criar("52998224725").Value).Value;
        cliente.AdicionarConta("00001", "123456789012");
        await Salvar(db => db.Clientes.Add(cliente));

        await using var escopo = Escopo();
        var repo = escopo.ServiceProvider.GetRequiredService<IClienteRepository>();
        var lido = await repo.ObterPorIdAsync(cliente.Id, default);

        Assert.NotNull(lido);
        Assert.Single(lido.Contas);
    }

    [Fact]
    public async Task ExisteComDocumento_RespondeCorretamente()
    {
        var doc = Documento.Criar("52998224725").Value;
        await Salvar(db => db.Clientes.Add(Cliente.Criar("Ana", doc).Value));

        await using var escopo = Escopo();
        var repo = escopo.ServiceProvider.GetRequiredService<IClienteRepository>();

        Assert.True(await repo.ExisteComDocumentoAsync(doc, default));
        Assert.False(await repo.ExisteComDocumentoAsync(Documento.Criar("11144477735").Value, default));
    }

    [Fact]
    public async Task ExisteAtiva_ComSpecificationNoBanco_RespondeCorretamente()
    {
        var clienteId = ClienteId.Novo();
        var data = DataReferencia.Criar(new DateOnly(2025, 1, 10), _relogio).Value;
        await Salvar(db => db.SolicitacoesArquivo.Add(SolicitacaoArquivo.Criar(clienteId, data, "ana", _relogio)));

        await using var escopo = Escopo();
        var repo = escopo.ServiceProvider.GetRequiredService<ISolicitacaoArquivoRepository>();

        Assert.True(await repo.ExisteAtivaAsync(clienteId, data, default));
        Assert.False(await repo.ExisteAtivaAsync(ClienteId.Novo(), data, default));
    }

    [Fact]
    public async Task ExisteAtiva_AposConcluir_RetornaFalso()
    {
        var clienteId = ClienteId.Novo();
        var data = DataReferencia.Criar(new DateOnly(2025, 1, 10), _relogio).Value;
        var solicitacao = SolicitacaoArquivo.Criar(clienteId, data, "ana", _relogio);
        solicitacao.IniciarProcessamento(_relogio);
        solicitacao.Concluir("extrato.txt", _relogio);
        await Salvar(db => db.SolicitacoesArquivo.Add(solicitacao));

        await using var escopo = Escopo();
        var repo = escopo.ServiceProvider.GetRequiredService<ISolicitacaoArquivoRepository>();

        Assert.False(await repo.ExisteAtivaAsync(clienteId, data, default));
    }

    [Fact]
    public async Task Transacoes_RetornaSomenteContaEDataPedidas_EmOrdem()
    {
        var conta = ContaId.Novo();
        var outraConta = ContaId.Novo();
        var dia = new DateOnly(2025, 1, 10);
        var valor = Dinheiro.Criar(10m).Value;

        await Salvar(db =>
        {
            db.Transacoes.Add(Transacao.Registrar(conta, dia, valor, TipoTransacao.Credito, "a").Value);
            db.Transacoes.Add(Transacao.Registrar(conta, dia, valor, TipoTransacao.Debito, "b").Value);
            db.Transacoes.Add(Transacao.Registrar(conta, dia.AddDays(1), valor, TipoTransacao.Credito, "fora da data").Value);
            db.Transacoes.Add(Transacao.Registrar(outraConta, dia, valor, TipoTransacao.Credito, "outra conta").Value);
        });

        await using var escopo = Escopo();
        var repo = escopo.ServiceProvider.GetRequiredService<ITransacaoRepository>();
        var data = DataReferencia.Criar(dia, _relogio).Value;

        var lidas = new List<string>();
        await foreach (var t in repo.ListarPorContasEDataAsync([conta], data, default))
            lidas.Add(t.Descricao);

        Assert.Equal(2, lidas.Count);
        Assert.DoesNotContain("fora da data", lidas);
        Assert.DoesNotContain("outra conta", lidas);
    }

    private sealed class RelogioTeste : IDateTimeProvider
    {
        public DateTime UtcNow => new(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        public DateOnly Hoje => DateOnly.FromDateTime(UtcNow);
    }
}
