using FileForge.Domain.ValueObjects;

namespace FileForge.Domain.Transacoes;

/// <summary>
/// Repositório do aggregate <see cref="Transacao"/>. A leitura para o arquivo é em <b>streaming</b>
/// (<see cref="IAsyncEnumerable{T}"/>): um cliente pode ter milhões de transações e não queremos carregar tudo em memória.
/// </summary>
public interface ITransacaoRepository
{
    IAsyncEnumerable<Transacao> ListarPorClienteEDataAsync(ClienteId clienteId, DataReferencia data, CancellationToken cancellationToken);

    void Adicionar(Transacao transacao);
}
