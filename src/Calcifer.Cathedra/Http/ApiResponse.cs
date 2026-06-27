namespace Calcifer.Cathedra.Http;

/// <summary>
/// The uniform response envelope for the entire platform. Success carries Data; failure carries
/// Error. CorrelationId ties the response to its log lines and the X-Correlation-ID header.
/// </summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public ApiError? Error { get; init; }
    public string? CorrelationId { get; set; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string code, string message) =>
        new() { Success = false, Error = new ApiError(code, message) };
}
