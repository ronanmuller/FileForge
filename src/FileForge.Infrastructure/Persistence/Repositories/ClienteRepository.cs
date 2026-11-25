using FileForge.Domain.Interfaces;
using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Infrastructure.Persistence.Repositories;

internal sealed class ClienteRepository(FileForgeDbContext dbContext) : IClienteRepository
{
    public Task<Cliente?> ObterPorIdAsync(ClienteId id, CancellationToken cancellationToken) =>
        dbContext.Clientes
            .Include(c => c.Contas)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> ExisteComDocumentoAsync(Documento documento, CancellationToken cancellationToken) =>
        dbContext.Clientes.AnyAsync(c => c.Documento == documento, cancellationToken);

    public void Adicionar(Cliente cliente) => dbContext.Clientes.Add(cliente);
}
