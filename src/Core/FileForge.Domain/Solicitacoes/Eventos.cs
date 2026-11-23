using FileForge.Domain.ValueObjects;
using FileForge.SharedKernel.Domain;

namespace FileForge.Domain.Solicitacoes;

public sealed record SolicitacaoArquivoCriada(SolicitacaoArquivoId SolicitacaoId, ClienteId ClienteId, DateOnly DataReferencia) : DomainEvent;

public sealed record ProcessamentoIniciado(SolicitacaoArquivoId SolicitacaoId) : DomainEvent;

public sealed record ArquivoGerado(SolicitacaoArquivoId SolicitacaoId, string NomeArquivo) : DomainEvent;

public sealed record ProcessamentoFalhou(SolicitacaoArquivoId SolicitacaoId, string Motivo, int Tentativas) : DomainEvent;
