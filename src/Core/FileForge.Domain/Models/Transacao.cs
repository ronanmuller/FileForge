using FileForge.Domain.Erros;
using FileForge.Domain.ValueObjects;
using FileForge.SharedKernel.Domain;
using FileForge.SharedKernel.Results;

namespace FileForge.Domain.Models;

/// <summary>Aggregate próprio: referencia a conta apenas por <see cref="ContaId"/>.</summary>
public sealed class Transacao : AggregateRoot<TransacaoId>
{
    private Transacao() { }

    private Transacao(TransacaoId id, ContaId contaId, DateOnly data, Dinheiro valor, TipoTransacao tipo, string descricao)
        : base(id)
    {
        ContaId = contaId;
        Data = data;
        Valor = valor;
        Tipo = tipo;
        Descricao = descricao;
    }

    public ContaId ContaId { get; private set; }
    public DateOnly Data { get; private set; }
    public Dinheiro Valor { get; private set; } = null!;
    public TipoTransacao Tipo { get; private set; }
    public string Descricao { get; private set; } = string.Empty;

    /// <summary>Valor com sinal: crédito positivo, débito negativo.</summary>
    public decimal ValorComSinal => Tipo == TipoTransacao.Credito ? Valor.Valor : -Valor.Valor;

    public static Result<Transacao> Registrar(ContaId contaId, DateOnly data, Dinheiro valor, TipoTransacao tipo, string? descricao)
    {
        ArgumentNullException.ThrowIfNull(valor);

        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo));

        if (valor == Dinheiro.Zero)
            return DomainErrors.Transacao.ValorZerado;

        if (string.IsNullOrWhiteSpace(descricao))
            return DomainErrors.Transacao.DescricaoObrigatoria;

        return new Transacao(TransacaoId.Novo(), contaId, data, valor, tipo, descricao.Trim());
    }
}
