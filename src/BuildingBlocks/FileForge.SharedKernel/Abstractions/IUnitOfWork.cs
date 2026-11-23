namespace FileForge.SharedKernel.Abstractions;

/// <summary>
/// Unit of Work: confirma, em uma única transação, todas as alterações feitas pelos repositórios.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
