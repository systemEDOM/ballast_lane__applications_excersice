namespace BallastLane.Application.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Unexpected
}

public sealed record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static Error Validation(IReadOnlyDictionary<string, string[]> fieldErrors)
        => new("validation_failed", "One or more validation errors occurred.", ErrorType.Validation, fieldErrors);

    public static Error Validation(string field, string message)
        => Validation(new Dictionary<string, string[]> { [field] = [message] });

    public static Error Validation(string message)
        => new("validation_failed", message, ErrorType.Validation);

    public static Error NotFound(string message)
        => new("not_found", message, ErrorType.NotFound);

    public static Error Conflict(string message)
        => new("conflict", message, ErrorType.Conflict);

    public static Error Unauthorized(string message)
        => new("invalid_credentials", message, ErrorType.Unauthorized);

    public static Error Forbidden(string message)
        => new("forbidden", message, ErrorType.Forbidden);

    public static Error Unexpected(string message)
        => new("unexpected_error", message, ErrorType.Unexpected);
}

public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Ok() => new(true, null);

    public static Result Fail(Error error) => new(false, error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(true, null) => _value = value;

    private Result(Error error) : base(false, error) => _value = default;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result does not carry a value.");

    public static Result<T> Ok(T value) => new(value);

    public static new Result<T> Fail(Error error) => new(error);
}
