using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using ClinicManagementApp.Api.Common;

namespace ClinicManagementApp.Api.Middleware;

public class SyncRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SyncRequestLoggingMiddleware> _logger;

    public SyncRequestLoggingMiddleware(RequestDelegate next, ILogger<SyncRequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api/v1/sync", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("/sync", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        string correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }
        if (correlationId.Length > 32)
        {
            correlationId = correlationId[..32];
        }

        context.Response.Headers["X-Correlation-Id"] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            var sw = Stopwatch.StartNew();
            bool isClientLogs = path.Contains("/sync/client-logs", StringComparison.OrdinalIgnoreCase);

            if (isClientLogs)
            {
                await _next(context);
                sw.Stop();

                var user = context.User?.FindFirst(ClaimTypes.Email)?.Value
                           ?? context.User?.FindFirst("email")?.Value
                           ?? context.User?.Identity?.Name
                           ?? "anon";

                _logger.LogInformation("SYNC {Method} {Path} user={User} → {StatusCode} in {ElapsedMs} ms | len={ContentLength}",
                    context.Request.Method,
                    path + context.Request.QueryString,
                    user,
                    context.Response.StatusCode,
                    sw.ElapsedMilliseconds,
                    context.Request.ContentLength ?? 0);
                return;
            }

            // Normal sync endpoint: log masked request and response bodies
            string reqBodyMasked = "";
            context.Request.EnableBuffering();
            if (context.Request.ContentLength is > 0 && context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
                using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
                {
                    var rawReq = await reader.ReadToEndAsync();
                    reqBodyMasked = SyncLogRedactor.RedactAndTruncate(rawReq, 2048);
                }
                context.Request.Body.Position = 0;
            }

            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();

                try
                {
                    responseBody.Position = 0;
                    string resBodyRaw = "";
                    using (var reader = new StreamReader(responseBody, Encoding.UTF8, leaveOpen: true))
                    {
                        char[] buffer = new char[4096];
                        int read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
                        resBodyRaw = new string(buffer, 0, read);
                    }

                    string resBodyMasked = SyncLogRedactor.RedactAndTruncate(resBodyRaw, 2048);

                    responseBody.Position = 0;
                    await responseBody.CopyToAsync(originalBodyStream);

                    var user = context.User?.FindFirst(ClaimTypes.Email)?.Value
                               ?? context.User?.FindFirst("email")?.Value
                               ?? context.User?.Identity?.Name
                               ?? "anon";

                    _logger.LogInformation("SYNC {Method} {Path} user={User} → {StatusCode} in {ElapsedMs} ms | req={Req} | res={Res}",
                        context.Request.Method,
                        path + context.Request.QueryString,
                        user,
                        context.Response.StatusCode,
                        sw.ElapsedMilliseconds,
                        reqBodyMasked,
                        resBodyMasked);
                }
                finally
                {
                    context.Response.Body = originalBodyStream;
                }
            }
        }
    }
}

