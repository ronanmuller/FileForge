using FileForge.Domain.Events;
using FileForge.Domain.Models;
using FileForge.Domain.Specifications;
using FileForge.Domain.UnitTests.Builders;
using FileForge.Domain.ValueObjects;

namespace FileForge.Domain.UnitTests.Aggregates;

public class SolicitacaoArquivoSpecsTests
{
    public static TheoryData<string> Status => ["Pendente", "Processando", "Concluida", "Falhou"];

    private static SolicitacaoArquivo Em(string status) => status switch
    {
        "Processando" => Um.Solicitacao().Processando().Build(),
        "Concluida" => Um.Solicitacao().Concluida().Build(),
        "Falhou" => Um.Solicitacao().Falhada().Build(),
        _ => Um.Solicitacao().Build(),
    };

    [Theory]
    [MemberData(nameof(Status))]
    public void Ativa_ConcordaComAPropriedadeDoAggregate(string status)
    {
        var s = Em(status);

        Assert.Equal(s.Ativa, SolicitacaoArquivoSpecs.Ativa.Compile()(s));
    }

    [Fact]
    public void AtivaDoClienteNaData_FiltraPorClienteDataEStatus()
    {
        var relogio = new RelogioFake();
        var cliente = ClienteId.Novo();
        var data = DataReferencia.Criar(new DateOnly(2025, 1, 10), relogio).Value;
        var regra = SolicitacaoArquivoSpecs.AtivaDoClienteNaData(cliente, data).Compile();

        Assert.True(regra(Um.Solicitacao().DoCliente(cliente).ParaData(new DateOnly(2025, 1, 10)).Build()));
        Assert.False(regra(Um.Solicitacao().DoCliente(cliente).ParaData(new DateOnly(2025, 1, 9)).Build()));
        Assert.False(regra(Um.Solicitacao().ParaData(new DateOnly(2025, 1, 10)).Build()));
        Assert.False(regra(Um.Solicitacao().DoCliente(cliente).ParaData(new DateOnly(2025, 1, 10)).Concluida().Build()));
    }
}
