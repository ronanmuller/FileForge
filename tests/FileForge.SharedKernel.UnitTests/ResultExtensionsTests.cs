using FileForge.SharedKernel.Results;

namespace FileForge.SharedKernel.UnitTests;

public class ResultExtensionsTests
{
    private static readonly Error Erro = new("Teste.Erro", "falhou");

    [Fact]
    public void Map_EmSucesso_DeveTransformarValor()
    {
        var result = Result.Success(2).Map(x => x * 10);

        Assert.Equal(20, result.Value);
    }

    [Fact]
    public void Map_EmFalha_DevePropagarErroSemExecutarFuncao()
    {
        var executou = false;

        var result = Result.Failure<int>(Erro).Map(x => { executou = true; return x; });

        Assert.False(executou);
        Assert.Equal(Erro, result.Error);
    }

    [Fact]
    public void Bind_DeveEncadearOperacaoQuePodeFalhar()
    {
        var result = Result.Success(5).Bind(x => x > 3 ? Result.Failure<string>(Erro) : Result.Success("ok"));

        Assert.True(result.IsFailure);
        Assert.Equal(Erro, result.Error);
    }

    [Fact]
    public void Match_DeveExecutarCaminhoCorreto()
    {
        Assert.Equal("ok:1", Result.Success(1).Match(v => $"ok:{v}", e => e.Code));
        Assert.Equal("Teste.Erro", Result.Failure<int>(Erro).Match(v => $"ok:{v}", e => e.Code));
    }
}
