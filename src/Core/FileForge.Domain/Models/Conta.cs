using FileForge.Domain.Erros;
using FileForge.Domain.ValueObjects;
using FileForge.SharedKernel.Domain;
using FileForge.SharedKernel.Results;

namespace FileForge.Domain.Models;

/// <summary>Entidade filha de <see cref="Cliente"/>; só é criada/alterada através do aggregate root.</summary>
public sealed class Conta : Entity<ContaId>
{
    private Conta() { }

    private Conta(ContaId id, string agencia, string numero) : base(id)
    {
        Agencia = agencia;
        Numero = numero;
    }

    public string Agencia { get; private set; } = string.Empty;
    public string Numero { get; private set; } = string.Empty;

    internal static Result<Conta> Criar(string? agencia, string? numero)
    {
        agencia = agencia?.Trim();
        numero = numero?.Trim();

        if (!SomenteDigitos(agencia, 5))
            return DomainErrors.Conta.AgenciaInvalida;

        if (!SomenteDigitos(numero, 12))
            return DomainErrors.Conta.NumeroInvalido;

        return new Conta(ContaId.Novo(), agencia!, numero!);
    }

    internal bool Mesma(string agencia, string numero) => Agencia == agencia && Numero == numero;

    private static bool SomenteDigitos(string? valor, int maximo) =>
        !string.IsNullOrEmpty(valor) && valor.Length <= maximo && valor.All(char.IsDigit);
}
