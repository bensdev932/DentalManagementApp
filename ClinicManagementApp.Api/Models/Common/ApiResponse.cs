namespace ClinicManagementApp.Api.Models.Common;

/// <summary>
/// Uniform generic response envelope ensuring predictable JSON schema across all endpoints.
/// </summary>
/// <typeparam name="T">Type of payload data.</typeparam>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IReadOnlyList<string>? Errors { get; init; }
    public string? TraceId { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public static ApiResponse<T> SuccessResult(T data, string message = "Operation completed successfully.") =>
        new()
        {
            Success = true,
            Message = message,
            Data = data,
            Errors = null,
            TimestampUtc = DateTime.UtcNow
        };

    public static ApiResponse<T> FailureResult(string message, IReadOnlyList<string>? errors = null, string? traceId = null) =>
        new()
        {
            Success = false,
            Message = message,
            Data = default,
            Errors = errors,
            TraceId = traceId,
            TimestampUtc = DateTime.UtcNow
        };

    public static ApiResponse<T> FailureResult(string message, string error, string? traceId = null) =>
        new()
        {
            Success = false,
            Message = message,
            Data = default,
            Errors = [error],
            TraceId = traceId,
            TimestampUtc = DateTime.UtcNow
        };
}

/// <summary>
/// Non-generic response envelope for endpoints that return status/message without a specific data payload.
/// </summary>
public class ApiResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string>? Errors { get; init; }
    public string? TraceId { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public static ApiResponse SuccessResult(string message = "Operation completed successfully.") =>
        new()
        {
            Success = true,
            Message = message,
            Errors = null,
            TimestampUtc = DateTime.UtcNow
        };

    public static ApiResponse FailureResult(string message, IReadOnlyList<string>? errors = null, string? traceId = null) =>
        new()
        {
            Success = false,
            Message = message,
            Errors = errors,
            TraceId = traceId,
            TimestampUtc = DateTime.UtcNow
        };

    public static ApiResponse FailureResult(string message, string error, string? traceId = null) =>
        new()
        {
            Success = false,
            Message = message,
            Errors = [error],
            TraceId = traceId,
            TimestampUtc = DateTime.UtcNow
        };
}

