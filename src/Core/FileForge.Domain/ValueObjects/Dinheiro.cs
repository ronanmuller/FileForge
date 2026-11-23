using FileForge.Domain.Erros;
using FileForge.SharedKernel.Domain;
using FileForge.SharedKernel.Results;

namespace FileForge.Domain.ValueObjects;

public sealed class Dinheiro : ValueObject
{
    public static readonly Dinheiro Zero = new(0m);

    private Dinheiro(decimal valor) => Valor = valor;

    public decimal Valor { get; }

    public static Result<Dinheiro> Criar(decimal valor) =>
        valor < 0
            ? DomainErrors.Dinheiro.Negativo
            : new Dinheiro(Math.Round(valor, 2, MidpointRounding.AwayFromZero));

    public static Dinheiro operator +(Dinheiro a, Dinheiro b) => new(a.Valor + b.Valor);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Valor;
    }

    public override string ToString() => Valor.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
}
