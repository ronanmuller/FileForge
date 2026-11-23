using FileForge.Mediator;
using FileForge.SharedKernel.Results;
using FluentValidation;

namespace FileForge.Application.Behaviors;

/// <summary>
/// Executa todos os <see cref="IValidator{T}"/> do request. Se houver erros, interrompe o pipeline e devolve
/// um único <see cref="ValidationError"/> com a lista completa (a API transforma em 400 com todos os campos).
/// Valida <b>formato da entrada</b>; regras de negócio continuam no Domain.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var lista = validators as IValidator<TRequest>[] ?? [.. validators];
        if (lista.Length == 0)
            return await next().ConfigureAwait(false);

        var contexto = new ValidationContext<TRequest>(request);
        var erros = new List<Error>();

        foreach (var validator in lista)
        {
            var resultado = await validator.ValidateAsync(contexto, cancellationToken).ConfigureAwait(false);
            erros.AddRange(resultado.Errors.Select(f => Error.Validation(
                string.IsNullOrEmpty(f.ErrorCode) ? f.PropertyName : $"{f.PropertyName}.{f.ErrorCode}",
                f.ErrorMessage)));
        }

        return erros.Count == 0
            ? await next().ConfigureAwait(false)
            : ResultFactory.Falha<TResponse>(new ValidationError(erros));
    }
}
