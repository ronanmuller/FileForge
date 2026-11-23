using System.Diagnostics;
using FileForge.Application.Logging;
using FileForge.Mediator;
using FileForge.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace FileForge.Application.Behaviors;

/// <summary>
/// Behavior mais externo do pipeline: loga início, fim, duração e o tipo de desfecho
/// (sucesso, falha de negócio via <see cref="Result"/> ou exceção). É o único lugar que loga
/// requisições — os handlers não precisam (e não devem) repetir isso.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    internal const int LimiteLentidaoMs = 500;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var nome = typeof(TRequest).Name;
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["RequestName"] = nome });

        logger.ProcessandoRequisicao(nome);
        var inicio = Stopwatch.GetTimestamp();

        try
        {
            var response = await next().ConfigureAwait(false);
            var ms = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;

            if (response is Result { IsFailure: true } falha)
                logger.RequisicaoFalhouNegocio(nome, ms, falha.Error.Code, falha.Error.Message);
            else
                logger.RequisicaoConcluida(nome, ms);

            if (ms > LimiteLentidaoMs)
                logger.RequisicaoLenta(nome, ms, LimiteLentidaoMs);

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.RequisicaoComExcecao(ex, nome, Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
            throw;
        }
    }
}
