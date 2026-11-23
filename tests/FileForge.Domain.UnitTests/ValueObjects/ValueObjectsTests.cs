using FileForge.Domain.Erros;
using FileForge.Domain.ValueObjects;
using FileForge.SharedKernel.Abstractions;

namespace FileForge.Domain.UnitTests.ValueObjects;

public class ClienteIdTests
{
    [Fact]
    public void Criar_ComGuidVazio_DeveFalhar()
    {
        var result = ClienteId.Criar(Guid.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Cliente.IdVazio, result.Error);
    }

    [Fact]
    public void Criar_ComMesmoGuid_DeveSerIgual()
    {
        var guid = Guid.NewGuid();

        Assert.Equal(ClienteId.Criar(guid).Value, ClienteId.Criar(guid).Value);
    }

    [Fact]
    public void Novo_DeveGerarIdsDiferentes() => Assert.NotEqual(ClienteId.Novo(), ClienteId.Novo());
}

public class DocumentoTests
{
    [Theory]
    [InlineData("529.982.247-25", "52998224725", TipoDocumento.Cpf)]
    [InlineData("52998224725", "52998224725", TipoDocumento.Cpf)]
    [InlineData("11.222.333/0001-81", "11222333000181", TipoDocumento.Cnpj)]
    public void Criar_ComDocumentoValido_DeveNormalizar(string entrada, string esperado, TipoDocumento tipo)
    {
        var result = Documento.Criar(entrada);

        Assert.True(result.IsSuccess);
        Assert.Equal(esperado, result.Value.Numero);
        Assert.Equal(tipo, result.Value.Tipo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_Vazio_DeveFalhar(string? entrada) =>
        Assert.Equal(DomainErrors.Documento.Vazio, Documento.Criar(entrada).Error);

    [Theory]
    [InlineData("52998224724")]
    [InlineData("11111111111")]
    [InlineData("11222333000180")]
    [InlineData("123")]
    public void Criar_Invalido_DeveFalhar(string entrada) =>
        Assert.Equal(DomainErrors.Documento.Invalido, Documento.Criar(entrada).Error);

    [Fact]
    public void Documentos_ComMesmoNumero_DevemSerIguais() =>
        Assert.Equal(Documento.Criar("529.982.247-25").Value, Documento.Criar("52998224725").Value);
}

public class DinheiroTests
{
    [Fact]
    public void Criar_Negativo_DeveFalhar() =>
        Assert.Equal(DomainErrors.Dinheiro.Negativo, Dinheiro.Criar(-0.01m).Error);

    [Fact]
    public void Criar_DeveArredondarParaDuasCasas() =>
        Assert.Equal(10.13m, Dinheiro.Criar(10.125m).Value.Valor);

    [Fact]
    public void Somar_DeveRetornarNovaInstancia()
    {
        var a = Dinheiro.Criar(10m).Value;
        var b = Dinheiro.Criar(5.5m).Value;

        var soma = a + b;

        Assert.Equal(15.5m, soma.Valor);
        Assert.Equal(10m, a.Valor);
    }

    [Fact]
    public void Valores_Iguais_DevemSerIguais() =>
        Assert.Equal(Dinheiro.Criar(1m).Value, Dinheiro.Criar(1.00m).Value);
}

public class DataReferenciaTests
{
    private static readonly IDateTimeProvider Relogio = new RelogioFake();

    [Fact]
    public void Criar_NoFuturo_DeveFalhar() =>
        Assert.Equal(DomainErrors.DataReferencia.NoFuturo, DataReferencia.Criar(new DateOnly(2025, 1, 16), Relogio).Error);

    [Theory]
    [InlineData(2025, 1, 15)]
    [InlineData(2024, 12, 31)]
    public void Criar_HojeOuPassado_DeveSerValida(int ano, int mes, int dia)
    {
        var result = DataReferencia.Criar(new DateOnly(ano, mes, dia), Relogio);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(ano, mes, dia), result.Value.Valor);
    }

    [Fact]
    public void Criar_As22hEmSaoPaulo_AmanhaEmUtc_AindaEhFuturo()
    {
        // 15/01 22:00 em São Paulo = 16/01 01:00 UTC. Para o negócio, 16/01 ainda é amanhã.
        var relogio = new RelogioFake(new DateTime(2025, 1, 16, 1, 0, 0, DateTimeKind.Utc));

        var result = DataReferencia.Criar(new DateOnly(2025, 1, 16), relogio);

        Assert.Equal(DomainErrors.DataReferencia.NoFuturo, result.Error);
    }
}
