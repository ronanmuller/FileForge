using FileForge.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FileForge.Infrastructure.Persistence;

/// <summary>
/// Implementação do Unit of Work sobre o DbContext: confirma, em uma única transação,
/// tudo o que os repositórios alteraram. Traduz a violação de unicidade do PostgreSQL (23505)
/// para <see cref="UnicidadeVioladaException"/>, para a Application não depender do provedor.
/// </summary>
public sealed class UnitOfWork(FileForgeDbContext dbContext) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new UnicidadeVioladaException(pg.ConstraintName ?? "desconhecida", ex);
        }
    }
}
