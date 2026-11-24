using FileForge.Domain.Erros;
using FileForge.Domain.Events;
using FileForge.Domain.Models;
using FileForge.Domain.Specifications;
using FileForge.Domain.ValueObjects;

namespace FileForge.Domain.UnitTests.Aggregates;

public class TransacaoTests
{
    private static readonly DateOnly Data = new(2025, 1, 10);

    [Fact]
    public void Registrar_ValorZerado_DeveFalhar() =>
        Assert.Equal(DomainErrors.Transacao.ValorZerado,
            Transacao.Registrar(ContaId.Novo(), Data, Dinheiro.Zero, TipoTransacao.Credito, "PIX").Error);

    [Fact]
    public void Registrar_SemDescricao_DeveFalhar() =>
        Assert.Equal(DomainErrors.Transacao.DescricaoObrigatoria,
            Transacao.Registrar(ContaId.Novo(), Data, Dinheiro.Criar(10m).Value, TipoTransacao.Credito, " ").Error);

    [Fact]
    public void Registrar_TipoInexistente_DeveLancarExcecao() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Transacao.Registrar(ContaId.Novo(), Data, Dinheiro.Criar(10m).Value, (TipoTransacao)99, "PIX"));

    [Theory]
    [InlineData(TipoTransacao.Credito, 50)]
    [InlineData(TipoTransacao.Debito, -50)]
    public void ValorComSinal_DependeDoTipo(TipoTransacao tipo, decimal esperado)
    {
        var transacao = Transacao.Registrar(ContaId.Novo(), Data, Dinheiro.Criar(50m).Value, tipo, "PIX").Value;

        Assert.Equal(esperado, transacao.ValorComSinal);
    }
}
