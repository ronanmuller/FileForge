using FileForge.Domain.Erros;
using FileForge.SharedKernel.Results;

namespace FileForge.Domain.ValueObjects;

public readonly record struct ClienteId(Guid Value)
{
    public static ClienteId Novo() => new(Guid.NewGuid());

    public static Result<ClienteId> Criar(Guid value) =>
        value == Guid.Empty ? DomainErrors.Cliente.IdVazio : new ClienteId(value);

    public override string ToString() => Value.ToString();
}
