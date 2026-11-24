using FileForge.Domain.Erros;
using FileForge.Domain.Events;
using FileForge.Domain.Models;
using FileForge.Domain.Specifications;
using FileForge.Domain.UnitTests.Builders;

namespace FileForge.Domain.UnitTests.Aggregates;

public class SolicitacaoArquivoTests
{
    private readonly RelogioFake _relogio = new();

    private SolicitacaoArquivo NovaSolicitacao() => Um.Solicitacao().ComRelogio(_relogio).Build();

    /// <summary>Tabela completa da máquina de status: cada transição, a partir de cada status.</summary>
    public static TheoryData<string, StatusSolicitacao, bool> Transicoes => new()
    {
        { "Iniciar",     StatusSolicitacao.Pendente,    true  },
        { "Iniciar",     StatusSolicitacao.Processando, false },
        { "Iniciar",     StatusSolicitacao.Concluida,   false },
        { "Iniciar",     StatusSolicitacao.Falhou,      false },
        { "Concluir",    StatusSolicitacao.Pendente,    false },
        { "Concluir",    StatusSolicitacao.Processando, true  },
        { "Concluir",    StatusSolicitacao.Concluida,   false },
        { "Concluir",    StatusSolicitacao.Falhou,      false },
        { "Falhar",      StatusSolicitacao.Pendente,    false },
        { "Falhar",      StatusSolicitacao.Processando, true  },
        { "Falhar",      StatusSolicitacao.Concluida,   false },
        { "Falhar",      StatusSolicitacao.Falhou,      false },
        { "Reprocessar", StatusSolicitacao.Pendente,    false },
        { "Reprocessar", StatusSolicitacao.Processando, false },
        { "Reprocessar", StatusSolicitacao.Concluida,   false },
        { "Reprocessar", StatusSolicitacao.Falhou,      true  },
    };

    [Theory]
    [MemberData(nameof(Transicoes))]
    public void MaquinaDeStatus_SoPermiteTransicoesValidas(string acao, StatusSolicitacao de, bool permitida)
    {
        var s = de switch
        {
            StatusSolicitacao.Processando => Um.Solicitacao().Processando().Build(),
            StatusSolicitacao.Concluida => Um.Solicitacao().Concluida().Build(),
            StatusSolicitacao.Falhou => Um.Solicitacao().Falhada().Build(),
            _ => Um.Solicitacao().Build(),
        };
        var eventosAntes = s.DomainEvents.Count;

        var result = acao switch
        {
            "Iniciar" => s.IniciarProcessamento(_relogio),
            "Concluir" => s.Concluir("a.txt", _relogio),
            "Falhar" => s.Falhar("erro", _relogio),
            _ => s.Reprocessar(_relogio),
        };

        Assert.Equal(permitida, result.IsSuccess);
        if (!permitida)
        {
            Assert.Equal(DomainErrors.SolicitacaoArquivo.TransicaoInvalida, result.Error);
            Assert.Equal(de, s.Status);
            Assert.Equal(eventosAntes, s.DomainEvents.Count);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Criar_SemQuemPediu_LancaExcecao(string? solicitadoPor) =>
        Assert.ThrowsAny<ArgumentException>(() => SolicitacaoArquivo.Criar(
            Domain.ValueObjects.ClienteId.Novo(),
            Domain.ValueObjects.DataReferencia.Criar(new DateOnly(2025, 1, 10), _relogio).Value,
            solicitadoPor!,
            _relogio));

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
        var s = Um.Solicitacao().Processando().Build();

        Assert.Equal(DomainErrors.SolicitacaoArquivo.TransicaoInvalida, s.IniciarProcessamento(_relogio).Error);
    }

    [Fact]
    public void Reprocessar_Concluida_DeveFalhar()
    {
        var s = Um.Solicitacao().Concluida().Build();

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

    [Fact]
    public void Eventos_UsamOHorarioDoRelogioInjetado()
    {
        var s = NovaSolicitacao();
        _relogio.UtcNow = _relogio.UtcNow.AddMinutes(5);
        s.IniciarProcessamento(_relogio);

        Assert.Equal(new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc), s.DomainEvents.First().OccurredOnUtc);
        Assert.Equal(new DateTime(2025, 1, 15, 12, 5, 0, DateTimeKind.Utc), s.DomainEvents.Last().OccurredOnUtc);
    }

    [Fact]
    public void IniciarProcessamento_RelogioNulo_LancaSemAlterarEstado()
    {
        var s = NovaSolicitacao();

        Assert.Throws<ArgumentNullException>(() => s.IniciarProcessamento(null!));

        Assert.Equal(StatusSolicitacao.Pendente, s.Status);
        Assert.Equal(0, s.Tentativas);
        Assert.Single(s.DomainEvents);
    }
}
