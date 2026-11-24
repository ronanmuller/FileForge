using FileForge.Domain.Events;
using FileForge.Domain.Models;
using FileForge.Domain.Specifications;
using FileForge.Domain.ValueObjects;

namespace FileForge.Domain.Interfaces;

/// <summary>
/// Repositório do aggregate <see cref="Transacao"/>. A leitura para o arquivo é em <b>streaming</b>
/// (<see cref="IAsyncEnumerable{T}"/>): um cliente pode ter milhões de transações e não queremos carregar tudo em memória.
/// </summary>
/// <remarks>
/// Recebe <see cref="ContaId"/>s (e não <c>ClienteId</c>) porque a <see cref="Transacao"/> só conhece a conta.
/// Quem sabe quais contas pertencem a um cliente é o aggregate <c>Cliente</c>: o handler carrega o cliente
/// e repassa os Ids. Assim este repositório não precisa conhecer a estrutura interna de outro aggregate.
/// </remarks>
public interface ITransacaoRepository
{
    IAsyncEnumerable<Transacao> ListarPorContasEDataAsync(
        IReadOnlyCollection<ContaId> contas, DataReferencia data, CancellationToken cancellationToken);

    void Adicionar(Transacao transacao);
}
