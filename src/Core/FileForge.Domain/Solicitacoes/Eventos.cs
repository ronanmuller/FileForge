using FileForge.Domain.ValueObjects;
using FileForge.SharedKernel.Domain;

namespace FileForge.Domain.Solicitacoes;

public sealed record SolicitacaoArquivoCriada(
    SolicitacaoArquivoId SolicitacaoId, ClienteId ClienteId, DateOnly DataReferencia, DateTime OccurredOnUtc)
    : DomainEvent(OccurredOnUtc);

public sealed record ProcessamentoIniciado(SolicitacaoArquivoId SolicitacaoId, int Tentativa, DateTime OccurredOnUtc)
    : DomainEvent(OccurredOnUtc);

public sealed record ArquivoGerado(SolicitacaoArquivoId SolicitacaoId, string NomeArquivo, DateTime OccurredOnUtc)
    : DomainEvent(OccurredOnUtc);

public sealed record ProcessamentoFalhou(SolicitacaoArquivoId SolicitacaoId, string Motivo, int Tentativas, DateTime OccurredOnUtc)
    : DomainEvent(OccurredOnUtc);
