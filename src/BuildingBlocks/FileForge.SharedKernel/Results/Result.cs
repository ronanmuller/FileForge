namespace FileForge.SharedKernel.Results;

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden
}

/// <summary>Não é <c>sealed</c> para permitir <see cref="ValidationError"/> (vários erros em um só).</summary>
public record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}

/// <summary>
/// Agrupa várias falhas de validação (ex.: um formulário com 3 campos inválidos) num único <see cref="Error"/>,
/// para que o <see cref="Result"/> continue com um só erro e a API devolva todos de uma vez (400 + lista).
/// </summary>
public sealed record ValidationError : Error
{
    public ValidationError(IReadOnlyList<Error> errors)
        : base("Validation.Failed", "Um ou mais erros de validação ocorreram.", ErrorType.Validation)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
            throw new ArgumentException("Informe pelo menos um erro.", nameof(errors));

        Errors = errors;
    }

    public IReadOnlyList<Error> Errors { get; }

    // Record compara listas por referência; aqui queremos comparar o conteúdo.
    public bool Equals(ValidationError? other) => base.Equals(other) && Errors.SequenceEqual(other!.Errors);

    public override int GetHashCode() => Errors.Aggregate(base.GetHashCode(), HashCode.Combine);
}

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None || !isSuccess && error == Error.None)
            throw new ArgumentException("Invalid result state.", nameof(error));

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default, false, error);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}
