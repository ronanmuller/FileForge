using FileForge.Domain.ValueObjects;

namespace FileForge.Domain.Solicitacoes;

/// <summary>
/// Repositório do aggregate <see cref="SolicitacaoArquivo"/>. Não confirma nada: quem faz commit é o
/// <c>IUnitOfWork</c>. A implementação (EF Core) fica na Infrastructure.
/// </summary>
public interface ISolicitacaoArquivoRepository
{
    Task<SolicitacaoArquivo?> ObterPorIdAsync(SolicitacaoArquivoId id, CancellationToken cancellationToken);

    /// <summary>Base da idempotência: já existe solicitação Pendente/Processando para o cliente na data?</summary>
    Task<bool> ExisteAtivaAsync(ClienteId clienteId, DataReferencia data, CancellationToken cancellationToken);

    void Adicionar(SolicitacaoArquivo solicitacao);
}
