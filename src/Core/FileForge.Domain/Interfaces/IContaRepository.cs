namespace FileForge.Domain.Interfaces;

/// <summary>
/// Consulta de contas no sistema inteiro. A unicidade de agência + número é global: uma conta existe uma única vez.
/// </summary>
public interface IContaRepository
{
    Task<bool> ExisteAsync(string agencia, string numero, CancellationToken cancellationToken);
}
