using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace FileForge.Mediator.Behaviors;

/// <summary>
/// Identificadores de telemetria nativa do <c>FileForge.Mediator</c>.
/// </summary>
public static class MediatorTelemetry
{
    public const string ActivitySourceName = "FileForge.Mediator";
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}

/// <summary>
/// Cria um span distribuído para cada <see cref="IRequest{TResponse}"/> executado no pipeline do Mediator.
/// </summary>
public sealed class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly string RequestName = typeof(TRequest).Name;
    private static readonly string OperationName = $"Mediator {RequestName}";
    private static readonly Func<TResponse, (bool IsResult, bool IsSuccess, string? ErrorCode)> ResultInspector =
        BuildResultInspector();

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        using var activity = MediatorTelemetry.ActivitySource.StartActivity(OperationName, ActivityKind.Internal);
        activity?.SetTag("mediator.request.name", RequestName);

        try
        {
            var response = await next().ConfigureAwait(false);

            if (activity is not null)
            {
                var (isResult, isSuccess, errorCode) = ResultInspector(response);
                if (isResult)
                {
                    activity.SetTag("mediator.result.is_success", isSuccess);
                    if (!isSuccess && !string.IsNullOrEmpty(errorCode))
                        activity.SetTag("error.code", errorCode);
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            if (activity is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.AddException(ex);
            }

            throw;
        }
    }

    private static Func<TResponse, (bool IsResult, bool IsSuccess, string? ErrorCode)> BuildResultInspector()
    {
        var responseType = typeof(TResponse);
        var isSuccessProp = responseType.GetProperty("IsSuccess", BindingFlags.Public | BindingFlags.Instance);
        var errorProp = responseType.GetProperty("Error", BindingFlags.Public | BindingFlags.Instance);

        if (isSuccessProp?.PropertyType != typeof(bool) || errorProp is null)
            return static _ => (false, true, null);

        var codeProp = errorProp.PropertyType.GetProperty("Code", BindingFlags.Public | BindingFlags.Instance);
        if (codeProp?.PropertyType != typeof(string))
            return static _ => (false, true, null);

        var param = Expression.Parameter(responseType, "response");
        var isSuccessAccess = Expression.Property(param, isSuccessProp);
        var errorAccess = Expression.Property(param, errorProp);
        var codeAccess = Expression.Property(errorAccess, codeProp);

        var tupleCtor = typeof(ValueTuple<bool, bool, string?>)
            .GetConstructor([typeof(bool), typeof(bool), typeof(string)])!;

        var newTuple = Expression.New(tupleCtor, Expression.Constant(true), isSuccessAccess, codeAccess);
        return Expression.Lambda<Func<TResponse, (bool, bool, string?)>>(newTuple, param).Compile();
    }
}
