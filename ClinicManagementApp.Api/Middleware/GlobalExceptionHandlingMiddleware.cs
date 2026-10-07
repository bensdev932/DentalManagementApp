using System.Diagnostics;
using System.Net;
using System.Text.Json;
using ClinicManagementApp.Api.Models.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClinicManagementApp.Api.Middleware;

/// <summary>
/// Intercepts unhandled exceptions across the HTTP pipeline and transforms them into standardized ApiResponse envelopes.
/// Catches 500 Internal Server Errors, database timeouts, concurrency issues, and maps them to clean HTTP status codes.
/// </summary>
public class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            logger.LogError(
                ex,
                "[TraceId: {TraceId}] Unhandled exception processing HTTP {Method} {Path}: {ErrorMessage}",
                traceId,
                context.Request.Method,
                context.Request.Path,
                ex.Message);

            if (context.Response.HasStarted)
            {
                logger.LogWarning(
                    "[TraceId: {TraceId}] Response already started. Cannot write exception payload.",
                    traceId);
                return;
            }

            await HandleExceptionAsync(context, ex, traceId);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, string traceId)
    {
        var (statusCode, userMessage) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, "The requested resource was not found."),
            ArgumentNullException or ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Access denied. Valid credentials required."),
            DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "A data concurrency conflict occurred. The resource was modified by another operation."),
            NpgsqlException or TimeoutException => (HttpStatusCode.ServiceUnavailable, "Database operation timed out or database server is temporarily unreachable."),
            _ => (HttpStatusCode.InternalServerError, "An internal server error occurred. Our team has been notified.")
        };

        var errors = new List<string>();

        if (environment.IsDevelopment())
        {
            errors.Add(exception.Message);
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                errors.Add(exception.StackTrace);
            }
        }
        else
        {
            errors.Add($"Error reference Trace ID: {traceId}");
        }

        var response = ApiResponse.FailureResult(userMessage, errors, traceId);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(response, JsonOptions);
        await context.Response.WriteAsync(json);
    }
}

