namespace FileForge.Infrastructure.Outbox;

/// <summary>
/// Mensagem gravada na mesma transação que o estado de negócio. O relay a publica depois,
/// então a publicação nunca fica para trás de uma escrita que já foi confirmada (ou o contrário).
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public OutboxMessage(Guid id, string tipo, string conteudo, DateTime criadaEmUtc)
    {
        Id = id;
        Tipo = tipo;
        Conteudo = conteudo;
        CriadaEmUtc = criadaEmUtc;
    }

    public Guid Id { get; private set; }
    public string Tipo { get; private set; } = string.Empty;
    public string Conteudo { get; private set; } = string.Empty;
    public DateTime CriadaEmUtc { get; private set; }
    public DateTime? PublicadaEmUtc { get; private set; }
    public int Tentativas { get; private set; }

    public bool Pendente => PublicadaEmUtc is null;

    public void MarcarPublicada(DateTime agoraUtc) => PublicadaEmUtc = agoraUtc;

    public void RegistrarFalha() => Tentativas++;
}
