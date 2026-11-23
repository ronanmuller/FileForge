using FileForge.Domain.Erros;
using FileForge.Domain.Solicitacoes;
using FileForge.Domain.ValueObjects;

namespace FileForge.Domain.UnitTests.Aggregates;

public class SolicitacaoArquivoTests
{
    private readonly RelogioFake _relogio = new();

    private SolicitacaoArquivo NovaSolicitacao() =>
        SolicitacaoArquivo.Criar(ClienteId.Novo(), DataReferencia.Criar(new DateOnly(2025, 1, 10), _relogio).Value, _relogio);

    [Fact]
    public void Criar_DeveFicarPendenteEAtiva_ERegistrarEvento()
    {
        var s = NovaSolicitacao();

        Assert.Equal(StatusSolicitacao.Pendente, s.Status);
        Assert.True(s.Ativa);
        Assert.IsType<SolicitacaoArquivoCriada>(Assert.Single(s.DomainEvents));
    }

    [Fact]
    public void FluxoFeliz_PendenteProcessandoConcluida()
    {
        var s = NovaSolicitacao();

        Assert.True(s.IniciarProcessamento(_relogio).IsSuccess);
        Assert.True(s.Concluir("extrato_20250110.txt", _relogio).IsSuccess);

        Assert.Equal(StatusSolicitacao.Concluida, s.Status);
        Assert.False(s.Ativa);
        Assert.Equal("extrato_20250110.txt", s.NomeArquivo);
        Assert.Equal(1, s.Tentativas);
        Assert.IsType<ArquivoGerado>(s.DomainEvents.Last());
    }

    [Fact]
    public void Falhar_EReprocessar_DeveVoltarParaPendenteEIncrementarTentativas()
    {
        var s = NovaSolicitacao();
        s.IniciarProcessamento(_relogio);

        Assert.True(s.Falhar("timeout no bucket", _relogio).IsSuccess);
        Assert.IsType<ProcessamentoFalhou>(s.DomainEvents.Last());

        Assert.True(s.Reprocessar(_relogio).IsSuccess);
        s.IniciarProcessamento(_relogio);

        Assert.Equal(StatusSolicitacao.Processando, s.Status);
        Assert.Equal(2, s.Tentativas);
    }

    [Fact]
    public void Concluir_SemEstarProcessando_DeveFalhar()
    {
        var s = NovaSolicitacao();

        var result = s.Concluir("a.txt", _relogio);

        Assert.Equal(DomainErrors.SolicitacaoArquivo.TransicaoInvalida, result.Error);
        Assert.Equal(StatusSolicitacao.Pendente, s.Status);
    }

    [Fact]
    public void IniciarProcessamento_DuasVezes_DeveFalhar()
    {
        var s = NovaSolicitacao();
        s.IniciarProcessamento(_relogio);

        Assert.Equal(DomainErrors.SolicitacaoArquivo.TransicaoInvalida, s.IniciarProcessamento(_relogio).Error);
    }

    [Fact]
    public void Reprocessar_Concluida_DeveFalhar()
    {
        var s = NovaSolicitacao();
        s.IniciarProcessamento(_relogio);
        s.Concluir("a.txt", _relogio);

        Assert.Equal(DomainErrors.SolicitacaoArquivo.TransicaoInvalida, s.Reprocessar(_relogio).Error);
    }

    [Fact]
    public void Concluir_SemNomeArquivo_DeveFalhar()
    {
        var s = NovaSolicitacao();
        s.IniciarProcessamento(_relogio);

        Assert.Equal(DomainErrors.SolicitacaoArquivo.NomeArquivoObrigatorio, s.Concluir("", _relogio).Error);
        Assert.Equal(StatusSolicitacao.Processando, s.Status);
    }

    [Fact]
    public void Falhar_SemMotivo_DeveFalhar()
    {
        var s = NovaSolicitacao();
        s.IniciarProcessamento(_relogio);

        Assert.Equal(DomainErrors.SolicitacaoArquivo.MotivoFalhaObrigatorio, s.Falhar(null, _relogio).Error);
    }
}
