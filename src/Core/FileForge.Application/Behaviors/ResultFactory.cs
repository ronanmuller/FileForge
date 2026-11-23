using System.Collections.Concurrent;
using System.Reflection;
using FileForge.SharedKernel.Results;

namespace FileForge.Application.Behaviors;

/// <summary>
/// Cria um <c>TResponse</c> de falha quando o behavior só conhece o tipo genérico
/// (<see cref="Result"/> ou <see cref="Result{T}"/>). A reflection roda uma vez por tipo; depois é delegate cacheado.
/// </summary>
internal static class ResultFactory
{
    private static readonly ConcurrentDictionary<Type, Func<Error, Result>> Fabricas = new();

    private static readonly MethodInfo FailureGenerico = typeof(Result)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m is { Name: nameof(Result.Failure), IsGenericMethodDefinition: true });

    public static TResponse Falha<TResponse>(Error error) where TResponse : Result =>
        (TResponse)Fabricas.GetOrAdd(typeof(TResponse), static tipo =>
        {
            if (tipo == typeof(Result))
                return Result.Failure;

            var valor = tipo.GetGenericArguments()[0];
            return FailureGenerico.MakeGenericMethod(valor).CreateDelegate<Func<Error, Result>>();
        })(error);
}
