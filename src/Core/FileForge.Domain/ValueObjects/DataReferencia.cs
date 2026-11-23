using FileForge.Domain.Erros;
using FileForge.SharedKernel.Abstractions;
using FileForge.SharedKernel.Domain;
using FileForge.SharedKernel.Results;

namespace FileForge.Domain.ValueObjects;

public sealed class DataReferencia : ValueObject
{
    private DataReferencia(DateOnly valor) => Valor = valor;

    public DateOnly Valor { get; }

    public static Result<DataReferencia> Criar(DateOnly valor, IDateTimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(relogio);

        return valor > relogio.Hoje
            ? DomainErrors.DataReferencia.NoFuturo
            : new DataReferencia(valor);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Valor;
    }

    public override string ToString() => Valor.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
