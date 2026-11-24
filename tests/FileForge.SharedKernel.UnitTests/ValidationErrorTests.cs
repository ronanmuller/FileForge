using FileForge.SharedKernel.Results;

namespace FileForge.SharedKernel.UnitTests;

public class ValidationErrorTests
{
    private static readonly Error CampoA = Error.Validation("A.Obrigatorio", "Informe A.");
    private static readonly Error CampoB = Error.Validation("B.Invalido", "B inválido.");

    [Fact]
    public void Criar_SemErros_LancaExcecao() =>
        Assert.Throws<ArgumentException>(() => new ValidationError([]));

    [Fact]
    public void Criar_DefineTipoValidationECodigoFixo()
    {
        var erro = new ValidationError([CampoA, CampoB]);

        Assert.Equal(ErrorType.Validation, erro.Type);
        Assert.Equal("Validation.Failed", erro.Code);
        Assert.Equal(2, erro.Errors.Count);
    }

    [Fact]
    public void Igualdade_ComparaOConteudoDaListaENaoAReferencia()
    {
        var a = new ValidationError([CampoA, CampoB]);
        var b = new ValidationError([CampoA, CampoB]);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Igualdade_ListasDiferentes_SaoDiferentes() =>
        Assert.NotEqual(new ValidationError([CampoA]), new ValidationError([CampoB]));

    [Fact]
    public void PodeSerUsadoComoFalhaDeResult()
    {
        Result result = new ValidationError([CampoA]);

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }
}
