using System.Collections.Concurrent;
using System.Reflection;
using FileForge.Application.Abstractions.Seguranca;
using FileForge.Application.Logging;
using FileForge.Mediator;
using FileForge.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace FileForge.Application.Behaviors;

/// <summary>
/// Verifica as permissões declaradas com <see cref="RequerPermissaoAttribute"/> <b>antes</b> de validar ou executar.
/// Falha vira <see cref="Result"/> (401/403 na API), não exceção. Autorização por <i>recurso</i>
/// ("este extrato é seu?") fica no handler, que é quem carrega o recurso.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    IUsuarioAtual usuario,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    private static readonly ConcurrentDictionary<Type, string[]> Cache = new();

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var exigidas = Cache.GetOrAdd(typeof(TRequest), static t =>
            [.. t.GetCustomAttributes<RequerPermissaoAttribute>().Select(a => a.Permissao)]);

        if (exigidas.Length == 0)
            return next();

        if (!usuario.Autenticado)
            return Task.FromResult(ResultFactory.Falha<TResponse>(ErrosDeSeguranca.NaoAutenticado));

        var faltando = exigidas.FirstOrDefault(p => !usuario.PossuiPermissao(p));
        if (faltando is not null)
        {
            logger.AcessoNegado(typeof(TRequest).Name, usuario.Id, faltando);
            return Task.FromResult(ResultFactory.Falha<TResponse>(ErrosDeSeguranca.SemPermissao(faltando)));
        }

        return next();
    }
}
