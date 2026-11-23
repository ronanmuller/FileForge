using FileForge.Domain.Erros;
using FileForge.Domain.ValueObjects;
using FileForge.SharedKernel.Domain;
using FileForge.SharedKernel.Results;

namespace FileForge.Domain.Clientes;

public sealed class Cliente : AggregateRoot<ClienteId>
{
    private readonly List<Conta> _contas = [];

    private Cliente() { }

    private Cliente(ClienteId id, string nome, Documento documento) : base(id)
    {
        Nome = nome;
        Documento = documento;
    }

    public string Nome { get; private set; } = string.Empty;
    public Documento Documento { get; private set; } = null!;
    public IReadOnlyCollection<Conta> Contas => _contas.AsReadOnly();

    public static Result<Cliente> Criar(string? nome, Documento documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        if (string.IsNullOrWhiteSpace(nome))
            return DomainErrors.Cliente.NomeObrigatorio;

        return new Cliente(ClienteId.Novo(), nome.Trim(), documento);
    }

    public Result<Conta> AdicionarConta(string? agencia, string? numero)
    {
        var conta = Conta.Criar(agencia, numero);
        if (conta.IsFailure)
            return conta;

        if (_contas.Any(c => c.Mesma(conta.Value.Agencia, conta.Value.Numero)))
            return DomainErrors.Cliente.ContaDuplicada;

        _contas.Add(conta.Value);
        return conta;
    }
}
