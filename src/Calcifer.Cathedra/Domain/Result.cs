using Calcifer.Cathedra.Http;

namespace Calcifer.Cathedra.Domain;

/// <summary>
/// The business-outcome type returned by services. A success carries a <see cref="Value"/>;
/// a failure carries an <see cref="ApiError"/> (Code + Message). <c>ApiResults.From</c> maps the
/// error code to an HTTP status, so the same <see cref="Result{T}"/> drives both the response
/// body and the status line.
/// </summary>
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public ApiError? Error { get; }

    private Result(bool isSuccess, T? value, ApiError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(ApiError error) => new(false, default, error);

    public static Result<T> Failure(string code, string message) =>
        new(false, default, new ApiError(code, message));

    /// <summary>Allow <c>return value;</c> from a method whose return type is <c>Result&lt;T&gt;</c>.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Allow <c>return error;</c> from a method whose return type is <c>Result&lt;T&gt;</c>.</summary>
    public static implicit operator Result<T>(ApiError error) => Failure(error);
}

/// <summary>Non-generic helpers for operations that succeed without producing a value.</summary>
public sealed class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public ApiError? Error { get; }

    private Result(bool isSuccess, ApiError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);

    public static Result Failure(ApiError error) => new(false, error);

    public static Result Failure(string code, string message) =>
        new(false, new ApiError(code, message));
}
