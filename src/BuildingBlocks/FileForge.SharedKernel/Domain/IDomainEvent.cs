namespace FileForge.SharedKernel.Domain;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}

/// <summary>
/// O instante é recebido de quem cria o evento (que usa <c>IDateTimeProvider</c>),
/// em vez de ler <c>DateTime.UtcNow</c> aqui — mantém os testes determinísticos.
/// </summary>
public abstract record DomainEvent(DateTime OccurredOnUtc) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
}
