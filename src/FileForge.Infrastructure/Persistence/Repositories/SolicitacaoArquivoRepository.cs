using FileForge.Domain.Interfaces;
using FileForge.Domain.Models;
using FileForge.Domain.Specifications;
using FileForge.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Infrastructure.Persistence.Repositories;

internal sealed class SolicitacaoArquivoRepository(FileForgeDbContext dbContext) : ISolicitacaoArquivoRepository
{
    public Task<SolicitacaoArquivo?> ObterPorIdAsync(SolicitacaoArquivoId id, CancellationToken cancellationToken) =>
        dbContext.SolicitacoesArquivo.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> ExisteAtivaAsync(ClienteId clienteId, DataReferencia data, CancellationToken cancellationToken) =>
        dbContext.SolicitacoesArquivo.AnyAsync(SolicitacaoArquivoSpecs.AtivaDoClienteNaData(clienteId, data), cancellationToken);

    public void Adicionar(SolicitacaoArquivo solicitacao) => dbContext.SolicitacoesArquivo.Add(solicitacao);
}
