namespace FileForge.SharedKernel.Results;

/// <summary>
/// Extensões funcionais para encadear operações sem <c>if (result.IsFailure)</c> repetido.
/// A falha "curto-circuita": as próximas etapas não são executadas e o erro é propagado.
/// </summary>
public static class ResultExtensions
{
    /// <summary>Transforma o valor em caso de sucesso.</summary>
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map) =>
        result.IsSuccess ? Result.Success(map(result.Value)) : Result.Failure<TOut>(result.Error);

    /// <summary>Encadeia outra operação que também pode falhar.</summary>
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> bind) =>
        result.IsSuccess ? bind(result.Value) : Result.Failure<TOut>(result.Error);

    /// <summary>Converte o resultado em um único valor, tratando os dois caminhos.</summary>
    public static TOut Match<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);

    /// <summary>Converte o resultado (sem valor) em um único valor, tratando os dois caminhos.</summary>
    public static TOut Match<TOut>(this Result result, Func<TOut> onSuccess, Func<Error, TOut> onFailure) =>
        result.IsSuccess ? onSuccess() : onFailure(result.Error);
}
