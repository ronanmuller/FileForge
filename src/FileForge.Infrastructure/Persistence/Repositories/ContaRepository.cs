using FileForge.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FileForge.Infrastructure.Persistence.Repositories;

internal sealed class ContaRepository(FileForgeDbContext dbContext) : IContaRepository
{
    public Task<bool> ExisteAsync(string agencia, string numero, CancellationToken cancellationToken) =>
        dbContext.Clientes
            .SelectMany(c => c.Contas)
            .AnyAsync(c => c.Agencia == agencia && c.Numero == numero, cancellationToken);
}
