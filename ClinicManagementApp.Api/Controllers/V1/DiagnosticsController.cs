using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementApp.Api.Controllers.V1;

/// <summary>
/// Diagnostic endpoints for system health verification and simulating server-side exceptions (500, 503).
/// </summary>
[ApiController]
[Route("api/v1/diagnostics")]
[AllowAnonymous]
public class DiagnosticsController(ApplicationDbContext dbContext, ILogger<DiagnosticsController> logger) : ControllerBase
{
    /// <summary>
    /// Checks API and PostgreSQL database health.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CheckHealth()
    {
        var canConnect = await dbContext.Database.CanConnectAsync();
        var healthData = new
        {
            Status = canConnect ? "Healthy" : "Degraded",
            Database = canConnect ? "Connected" : "Unreachable",
            ServerTimeUtc = DateTime.UtcNow
        };

        return canConnect
            ? Ok(ApiResponse<object>.SuccessResult(healthData, "API and database are operational."))
            : StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.FailureResult("Database connection check failed.", "Unable to connect to PostgreSQL."));
    }

    /// <summary>
    /// Intentionally throws an unhandled server exception to demonstrate 500 Internal Server Error handling.
    /// </summary>
    [HttpGet("simulate-500")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status500InternalServerError)]
    public IActionResult SimulateServerError([FromQuery] string? reason = null)
    {
        logger.LogInformation("Simulating an unexpected 500 server exception for diagnostic testing...");
        throw new ApplicationException(
            $"[SIMULATED 500 ERROR] Diagnostic test triggered. Reason: {reason ?? "Simulated unexpected server failure"}");
    }

    /// <summary>
    /// Intentionally simulates a database timeout / connection failure (503 Service Unavailable).
    /// </summary>
    [HttpGet("simulate-db-error")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status503ServiceUnavailable)]
    public IActionResult SimulateDatabaseTimeout()
    {
        logger.LogInformation("Simulating a database connection timeout for diagnostic testing...");
        throw new TimeoutException(
            "[SIMULATED 503 ERROR] Database connection pool timed out while acquiring an open connection.");
    }
}

