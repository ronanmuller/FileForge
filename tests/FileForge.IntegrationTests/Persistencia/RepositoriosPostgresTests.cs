using FileForge.Domain.Interfaces;
using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;
using FileForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FileForge.IntegrationTests.Persistencia;

/// <summary>
/// Repositórios contra um PostgreSQL real. Configuração e ciclo de vida vêm de <see cref="PostgresFixture"/>.
/// </summary>
public sealed class RepositoriosPostgresTests() : PostgresFixture("fileforge_test_repositorios")
{
    private RelogioTeste _relogio => Relogio;

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
    public async Task ExisteConta_PorAgenciaENumero_IndependenteDoCliente()
    {
        var cliente = Cliente.Criar("Ana", Documento.Criar("52998224725").Value).Value;
        cliente.AdicionarConta("00001", "123456789012");
        await Salvar(db => db.Clientes.Add(cliente));

        await using var escopo = Escopo();
        var repo = escopo.ServiceProvider.GetRequiredService<IContaRepository>();

        Assert.True(await repo.ExisteAsync("00001", "123456789012", default));
        Assert.False(await repo.ExisteAsync("00002", "123456789012", default));
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
}
