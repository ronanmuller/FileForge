using Microsoft.Extensions.Logging;

namespace FileForge.Application.Logging;

/// <summary>
/// Catálogo de logs da camada Application.
/// Usa o source generator <see cref="LoggerMessageAttribute"/>: sem boxing, sem parse de template em runtime,
/// e o nível é verificado antes de montar a mensagem. Cada log tem um <c>EventId</c> fixo, pesquisável
/// em qualquer ferramenta de observabilidade (Seq, Grafana Loki, Application Insights...).
/// </summary>
/// <remarks>
/// <c>internal</c>: cada camada tem o seu próprio catálogo e só usa o dela.
/// Faixas por camada: Application 1000-1999 | Infrastructure 2000-2999 | Worker 3000-3999 | API 4000-4999.
/// Dentro da Application: 1000-1099 pipeline | 1200-1299 segurança.
/// Um teste de arquitetura garante que não há EventId repetido nem fora da faixa.
/// </remarks>
internal static partial class LogDefinitions
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information,
        Message = "Processando {RequestName}")]
    public static partial void ProcessandoRequisicao(this ILogger logger, string requestName);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "{RequestName} concluída em {ElapsedMs} ms")]
    public static partial void RequisicaoConcluida(this ILogger logger, string requestName, double elapsedMs);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning,
        Message = "{RequestName} falhou por regra de negócio em {ElapsedMs} ms: {ErrorCode} - {ErrorMessage}")]
    public static partial void RequisicaoFalhouNegocio(
        this ILogger logger, string requestName, double elapsedMs, string errorCode, string errorMessage);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error,
        Message = "{RequestName} lançou exceção após {ElapsedMs} ms")]
    public static partial void RequisicaoComExcecao(this ILogger logger, Exception exception, string requestName, double elapsedMs);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning,
        Message = "{RequestName} lenta: {ElapsedMs} ms (limite {LimiteMs} ms)")]
    public static partial void RequisicaoLenta(this ILogger logger, string requestName, double elapsedMs, int limiteMs);

    [LoggerMessage(EventId = 1200, Level = LogLevel.Warning,
        Message = "Acesso negado a {RequestName} para o usuário {UserId}: falta a permissão {Permissao}")]
    public static partial void AcessoNegado(this ILogger logger, string requestName, string userId, string permissao);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Warning,
        Message = "Usuário {UserId} tentou acessar recurso {RecursoId} de outro cliente")]
    public static partial void AcessoNegadoARecurso(this ILogger logger, string userId, Guid recursoId);
}
