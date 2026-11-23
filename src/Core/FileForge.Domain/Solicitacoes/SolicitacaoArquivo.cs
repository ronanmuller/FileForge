using FileForge.Domain.Erros;
using FileForge.Domain.ValueObjects;
using FileForge.SharedKernel.Abstractions;
using FileForge.SharedKernel.Domain;
using FileForge.SharedKernel.Results;

namespace FileForge.Domain.Solicitacoes;

public enum StatusSolicitacao
{
    Pendente,
    Processando,
    Concluida,
    Falhou
}

/// <summary>
/// Centro do domínio. Máquina de status:
/// Pendente → Processando → Concluida | Falhou; Falhou → Pendente (reprocessar).
/// </summary>
public sealed class SolicitacaoArquivo : AggregateRoot<SolicitacaoArquivoId>
{
    private SolicitacaoArquivo() { }

    private SolicitacaoArquivo(SolicitacaoArquivoId id, ClienteId clienteId, DataReferencia dataReferencia, DateTime criadaEmUtc)
        : base(id)
    {
        ClienteId = clienteId;
        DataReferencia = dataReferencia;
        Status = StatusSolicitacao.Pendente;
        CriadaEmUtc = criadaEmUtc;
    }

    public ClienteId ClienteId { get; private set; }
    public DataReferencia DataReferencia { get; private set; } = null!;
    public StatusSolicitacao Status { get; private set; }
    public DateTime CriadaEmUtc { get; private set; }
    public DateTime? AtualizadaEmUtc { get; private set; }
    public string? NomeArquivo { get; private set; }
    public string? MotivoFalha { get; private set; }
    public int Tentativas { get; private set; }

    /// <summary>Ativa = ainda não concluída nem falhada; base da idempotência por cliente/data.</summary>
    public bool Ativa => Status is StatusSolicitacao.Pendente or StatusSolicitacao.Processando;

    public static SolicitacaoArquivo Criar(ClienteId clienteId, DataReferencia dataReferencia, IDateTimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(dataReferencia);
        ArgumentNullException.ThrowIfNull(relogio);

        var agora = relogio.UtcNow;
        var solicitacao = new SolicitacaoArquivo(SolicitacaoArquivoId.Novo(), clienteId, dataReferencia, agora);
        solicitacao.RaiseDomainEvent(new SolicitacaoArquivoCriada(solicitacao.Id, clienteId, dataReferencia.Valor, agora));
        return solicitacao;
    }

    public Result IniciarProcessamento(IDateTimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(relogio);

        if (Status != StatusSolicitacao.Pendente)
            return DomainErrors.SolicitacaoArquivo.TransicaoInvalida;

        Status = StatusSolicitacao.Processando;
        Tentativas++;
        AtualizadaEmUtc = relogio.UtcNow;
        RaiseDomainEvent(new ProcessamentoIniciado(Id, Tentativas, AtualizadaEmUtc.Value));
        return Result.Success();
    }

    public Result Concluir(string? nomeArquivo, IDateTimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(relogio);

        if (Status != StatusSolicitacao.Processando)
            return DomainErrors.SolicitacaoArquivo.TransicaoInvalida;

        if (string.IsNullOrWhiteSpace(nomeArquivo))
            return DomainErrors.SolicitacaoArquivo.NomeArquivoObrigatorio;

        Status = StatusSolicitacao.Concluida;
        NomeArquivo = nomeArquivo.Trim();
        MotivoFalha = null;
        AtualizadaEmUtc = relogio.UtcNow;
        RaiseDomainEvent(new ArquivoGerado(Id, NomeArquivo, AtualizadaEmUtc.Value));
        return Result.Success();
    }

    public Result Falhar(string? motivo, IDateTimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(relogio);

        if (Status != StatusSolicitacao.Processando)
            return DomainErrors.SolicitacaoArquivo.TransicaoInvalida;

        if (string.IsNullOrWhiteSpace(motivo))
            return DomainErrors.SolicitacaoArquivo.MotivoFalhaObrigatorio;

        Status = StatusSolicitacao.Falhou;
        MotivoFalha = motivo.Trim();
        AtualizadaEmUtc = relogio.UtcNow;
        RaiseDomainEvent(new ProcessamentoFalhou(Id, MotivoFalha, Tentativas, AtualizadaEmUtc.Value));
        return Result.Success();
    }

    public Result Reprocessar(IDateTimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(relogio);

        if (Status != StatusSolicitacao.Falhou)
            return DomainErrors.SolicitacaoArquivo.TransicaoInvalida;

        Status = StatusSolicitacao.Pendente;
        AtualizadaEmUtc = relogio.UtcNow;
        return Result.Success();
    }
}
