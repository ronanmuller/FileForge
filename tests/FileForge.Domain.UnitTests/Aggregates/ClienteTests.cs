using FileForge.Domain.Clientes;
using FileForge.Domain.Erros;
using FileForge.Domain.ValueObjects;

namespace FileForge.Domain.UnitTests.Aggregates;

public class ClienteTests
{
    private static Cliente NovoCliente() =>
        Cliente.Criar("Maria", Documento.Criar("52998224725").Value).Value;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Criar_SemNome_DeveFalhar(string? nome) =>
        Assert.Equal(DomainErrors.Cliente.NomeObrigatorio, Cliente.Criar(nome, Documento.Criar("52998224725").Value).Error);

    [Fact]
    public void Criar_Valido_DeveIniciarSemContas()
    {
        var cliente = NovoCliente();

        Assert.Equal("Maria", cliente.Nome);
        Assert.Empty(cliente.Contas);
    }

    [Fact]
    public void AdicionarConta_Valida_DeveAdicionar()
    {
        var cliente = NovoCliente();

        var result = cliente.AdicionarConta("0001", "123456");

        Assert.True(result.IsSuccess);
        Assert.Single(cliente.Contas);
    }

    [Fact]
    public void AdicionarConta_Duplicada_DeveFalhar()
    {
        var cliente = NovoCliente();
        cliente.AdicionarConta("0001", "123456");

        var result = cliente.AdicionarConta(" 0001 ", "123456");

        Assert.Equal(DomainErrors.Cliente.ContaDuplicada, result.Error);
        Assert.Single(cliente.Contas);
    }

    [Theory]
    [InlineData("", "123", "Conta.AgenciaInvalida")]
    [InlineData("12A", "123", "Conta.AgenciaInvalida")]
    [InlineData("123456", "123", "Conta.AgenciaInvalida")]
    [InlineData("0001", "", "Conta.NumeroInvalido")]
    [InlineData("0001", "12-3", "Conta.NumeroInvalido")]
    public void AdicionarConta_Invalida_DeveFalhar(string agencia, string numero, string codigo)
    {
        var cliente = NovoCliente();

        var result = cliente.AdicionarConta(agencia, numero);

        Assert.Equal(codigo, result.Error.Code);
        Assert.Empty(cliente.Contas);
    }
}
