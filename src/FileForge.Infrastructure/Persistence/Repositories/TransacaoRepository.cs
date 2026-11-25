using System.Runtime.CompilerServices;
using FileForge.Domain.Interfaces;
using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Infrastructure.Persistence.Repositories;

internal sealed class TransacaoRepository(FileForgeDbContext dbContext) : ITransacaoRepository
{
    public async IAsyncEnumerable<Transacao> ListarPorContasEDataAsync(
        IReadOnlyCollection<ContaId> contas,
        DataReferencia data,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var ids = contas.ToArray();

        var consulta = dbContext.Transacoes
            .AsNoTracking()
            .Where(t => ids.Contains(t.ContaId) && t.Data == data.Valor)
            .OrderBy(t => t.Data)
            .ThenBy(t => t.Id)
            .AsAsyncEnumerable();

        await foreach (var transacao in consulta.WithCancellation(cancellationToken))
            yield return transacao;
    }

    public void Adicionar(Transacao transacao) => dbContext.Transacoes.Add(transacao);
}
