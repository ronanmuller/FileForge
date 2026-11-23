namespace FileForge.Domain.ValueObjects;

public readonly record struct ContaId(Guid Value)
{
    public static ContaId Novo() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct TransacaoId(Guid Value)
{
    public static TransacaoId Novo() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct SolicitacaoArquivoId(Guid Value)
{
    public static SolicitacaoArquivoId Novo() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public enum TipoTransacao
{
    Credito,
    Debito
}
