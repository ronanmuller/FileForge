using FileForge.Domain.Clientes;
using FileForge.Domain.Solicitacoes;
using FileForge.Domain.ValueObjects;

namespace FileForge.Domain.UnitTests.Estudo;

/// <summary>
/// Testes didáticos: demonstram na prática os conceitos do GUIA-DO-PROJETO.md.
/// Coloque breakpoints e rode em Debug para acompanhar.
/// </summary>
public class IgualdadeEInvariantesTests
{
    // ---------- Value Object: igualdade por VALOR ----------

    [Fact]
    public void ValueObject_DuasInstanciasComMesmoValor_SaoIguais()
    {
        var a = Dinheiro.Criar(10m).Value;
        var b = Dinheiro.Criar(10m).Value;

        Assert.False(ReferenceEquals(a, b)); // objetos diferentes na memória...
        Assert.Equal(a, b);                  // ...mas iguais pelo valor
        Assert.True(a == b);
    }

    [Fact]
    public void ValueObject_EhImutavel_OperacaoGeraNovaInstancia()
    {
        var original = Dinheiro.Criar(10m).Value;

        var soma = original + Dinheiro.Criar(5m).Value;

        Assert.Equal(10m, original.Valor);
        Assert.Equal(15m, soma.Valor);
    }

    // ---------- Entity: igualdade por IDENTIDADE ----------

    [Fact]
    public void Entity_MesmosDados_IdsDiferentes_SaoDiferentes()
    {
        var doc = Documento.Criar("52998224725").Value;

        var maria1 = Cliente.Criar("Maria", doc).Value;
        var maria2 = Cliente.Criar("Maria", doc).Value;

        Assert.NotEqual(maria1, maria2); // mesmo nome e documento, mas são clientes distintos
    }

    // ---------- IDs fortemente tipados ----------

    [Fact]
    public void IdTipado_RecordStruct_TemIgualdadePorValor()
    {
        var guid = Guid.NewGuid();

        Assert.Equal(new ContaId(guid), new ContaId(guid));
        // ContaId x = new ClienteId(guid);  // <- não compila: é isso que o tipo forte garante
    }

    // ---------- Invariantes protegidas pelo aggregate ----------

    [Fact]
    public void Aggregate_ColecaoInternaNaoPodeSerAlteradaPorFora()
    {
        var cliente = Cliente.Criar("Maria", Documento.Criar("52998224725").Value).Value;

        // Contas é IReadOnlyCollection: não existe Add. A única porta é AdicionarConta.
        Assert.IsAssignableFrom<IReadOnlyCollection<Conta>>(cliente.Contas);
        Assert.False(cliente.Contas is ICollection<Conta> { IsReadOnly: false });
    }

    [Fact]
    public void Aggregate_FalhaDeNegocio_NaoAlteraEstado()
    {
        var relogio = new RelogioFake();
        var s = SolicitacaoArquivo.Criar(ClienteId.Novo(), DataReferencia.Criar(new DateOnly(2025, 1, 1), relogio).Value, relogio);
        var eventosAntes = s.DomainEvents.Count;

        var result = s.Concluir("a.txt", relogio); // pulando o Processando

        Assert.True(result.IsFailure);
        Assert.Equal(StatusSolicitacao.Pendente, s.Status);
        Assert.Equal(eventosAntes, s.DomainEvents.Count); // nenhum evento indevido
    }

    // ---------- Por que Value Object com invariante NÃO é record ----------

    private sealed record DinheiroComoRecord(decimal Valor);

    [Fact]
    public void ArmadilhaDoRecord_ConstrutorPublicoEWithBurlamValidacao()
    {
        var invalido = new DinheiroComoRecord(-50m);  // nada impede
        var copia = invalido with { Valor = -999m };  // nem o "with"

        Assert.Equal(-999m, copia.Valor);
        // Com Dinheiro (class : ValueObject) isso é impossível: só existe Dinheiro.Criar.
    }

    // ---------- Domain Events ----------

    [Fact]
    public void DomainEvents_SaoAcumuladosEDepoisLimpos()
    {
        var relogio = new RelogioFake();
        var s = SolicitacaoArquivo.Criar(ClienteId.Novo(), DataReferencia.Criar(new DateOnly(2025, 1, 1), relogio).Value, relogio);
        s.IniciarProcessamento(relogio);
        s.Concluir("a.txt", relogio);

        Assert.Collection(s.DomainEvents,
            e => Assert.IsType<SolicitacaoArquivoCriada>(e),
            e => Assert.IsType<ProcessamentoIniciado>(e),
            e => Assert.IsType<ArquivoGerado>(e));

        s.ClearDomainEvents(); // feito pela infra após publicar (Outbox)

        Assert.Empty(s.DomainEvents);
    }
}
