using FileForge.SharedKernel.Results;

namespace FileForge.SharedKernel.UnitTests;

public class ResultTests
{
    private static readonly Error ErroQualquer = new("Teste.Erro", "Algo deu errado");

    [Fact]
    public void Success_DeveSerSucessoSemErro()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_DeveSerFalhaComErro()
    {
        var result = Result.Failure(ErroQualquer);

        Assert.True(result.IsFailure);
        Assert.Equal(ErroQualquer, result.Error);
    }

    [Fact]
    public void Failure_ComErrorNone_DeveLancarArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
    }

    [Fact]
    public void SuccessGenerico_DeveExporValor()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void FailureGenerico_AcessarValue_DeveLancarInvalidOperationException()
    {
        var result = Result.Failure<int>(ErroQualquer);

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ConversaoImplicita_DeValor_DeveGerarSucesso()
    {
        Result<string> result = "ok";

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public void ConversaoImplicita_DeErro_DeveGerarFalha()
    {
        Result<string> result = ErroQualquer;

        Assert.True(result.IsFailure);
        Assert.Equal(ErroQualquer, result.Error);
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    public void FabricasDeErro_DevemDefinirTipoCorreto(ErrorType tipo)
    {
        var error = tipo switch
        {
            ErrorType.Validation => Error.Validation("c", "m"),
            ErrorType.NotFound => Error.NotFound("c", "m"),
            _ => Error.Conflict("c", "m")
        };

        Assert.Equal(tipo, error.Type);
    }
}
