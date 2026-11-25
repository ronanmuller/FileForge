using FileForge.SharedKernel.Abstractions;

namespace FileForge.Infrastructure.Persistence;

/// <summary>
/// Implementação do Unit of Work sobre o DbContext: confirma, em uma única transação,
/// tudo o que os repositórios alteraram no contexto.
/// </summary>
public sealed class UnitOfWork(FileForgeDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
