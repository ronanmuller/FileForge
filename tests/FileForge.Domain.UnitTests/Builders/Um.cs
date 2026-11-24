using FileForge.Domain.Events;
using FileForge.Domain.Models;
using FileForge.Domain.Specifications;
using FileForge.Domain.ValueObjects;

namespace FileForge.Domain.UnitTests.Builders;

/// <summary>
/// Test Data Builders: cada teste declara só o que importa para ele; o resto tem valor padrão válido.
/// Ex.: <c>Um.Cliente().ComConta("0001", "123").Build()</c>.
/// </summary>
internal static class Um
{
    public static ClienteBuilder Cliente() => new();
    public static TransacaoBuilder Transacao() => new();
    public static SolicitacaoBuilder Solicitacao() => new();
}

internal sealed class ClienteBuilder
{
    private string _nome = "Maria da Silva";
    private string _documento = "52998224725";
    private readonly List<(string Agencia, string Numero)> _contas = [];

    public ClienteBuilder ComNome(string nome) { _nome = nome; return this; }
    public ClienteBuilder ComDocumento(string documento) { _documento = documento; return this; }
    public ClienteBuilder ComConta(string agencia = "0001", string numero = "123456") { _contas.Add((agencia, numero)); return this; }

    public Cliente Build()
    {
        var cliente = Models.Cliente.Criar(_nome, Documento.Criar(_documento).Value).Value;
        foreach (var (agencia, numero) in _contas)
            _ = cliente.AdicionarConta(agencia, numero).Value;
        return cliente;
    }
}

internal sealed class TransacaoBuilder
{
    private ContaId _contaId = ContaId.Novo();
    private DateOnly _data = new(2025, 1, 10);
    private decimal _valor = 100m;
    private TipoTransacao _tipo = TipoTransacao.Credito;
    private string _descricao = "PIX recebido";

    public TransacaoBuilder DaConta(ContaId contaId) { _contaId = contaId; return this; }
    public TransacaoBuilder Em(DateOnly data) { _data = data; return this; }
    public TransacaoBuilder DeValor(decimal valor) { _valor = valor; return this; }
    public TransacaoBuilder Debito() { _tipo = TipoTransacao.Debito; return this; }
    public TransacaoBuilder Credito() { _tipo = TipoTransacao.Credito; return this; }
    public TransacaoBuilder ComDescricao(string descricao) { _descricao = descricao; return this; }

    public Transacao Build() =>
        Models.Transacao.Registrar(_contaId, _data, Dinheiro.Criar(_valor).Value, _tipo, _descricao).Value;
}

internal sealed class SolicitacaoBuilder
{
    private ClienteId _clienteId = ClienteId.Novo();
    private DateOnly _data = new(2025, 1, 10);
    private RelogioFake _relogio = new();
    private StatusSolicitacao _status = StatusSolicitacao.Pendente;
    private string _solicitadoPor = "usuario-teste";

    public SolicitacaoBuilder SolicitadaPor(string usuario) { _solicitadoPor = usuario; return this; }

    public SolicitacaoBuilder DoCliente(ClienteId clienteId) { _clienteId = clienteId; return this; }
    public SolicitacaoBuilder ParaData(DateOnly data) { _data = data; return this; }
    public SolicitacaoBuilder ComRelogio(RelogioFake relogio) { _relogio = relogio; return this; }

    public SolicitacaoBuilder Processando() { _status = StatusSolicitacao.Processando; return this; }
    public SolicitacaoBuilder Concluida() { _status = StatusSolicitacao.Concluida; return this; }
    public SolicitacaoBuilder Falhada() { _status = StatusSolicitacao.Falhou; return this; }

    /// <summary>Leva a solicitação ao status desejado passando pelas transições reais (não "força" o estado).</summary>
    public SolicitacaoArquivo Build()
    {
        var s = SolicitacaoArquivo.Criar(_clienteId, DataReferencia.Criar(_data, _relogio).Value, _solicitadoPor, _relogio);

        if (_status is StatusSolicitacao.Pendente)
            return s;

        _ = s.IniciarProcessamento(_relogio);

        if (_status is StatusSolicitacao.Concluida)
            _ = s.Concluir("extrato.txt", _relogio);
        else if (_status is StatusSolicitacao.Falhou)
            _ = s.Falhar("erro simulado", _relogio);

        return s;
    }
}
