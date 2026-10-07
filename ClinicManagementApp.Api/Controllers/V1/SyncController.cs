using System.Security.Claims;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Sync;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClinicManagementApp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/sync")]
[Authorize]
public class SyncController(ISyncService syncService, IClientLogService clientLogService) : ControllerBase
{
    [HttpPost("push")]
    [ProducesResponseType(typeof(ApiResponse<SyncPushResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SyncPushResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Push([FromBody] SyncPushRequest request)
    {
        var result = await syncService.ProcessPushRecordAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("batch")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SyncBatchItemResult>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Batch([FromBody] IReadOnlyList<SyncPushRequest> requests)
    {
        var result = await syncService.ProcessBatchAsync(requests);
        return Ok(result);
    }

    [HttpGet("pull")]
    [ProducesResponseType(typeof(ApiResponse<SyncPullResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pull([FromQuery] DateTime? since)
    {
        var result = await syncService.PullDeltaAsync(since);
        return Ok(result);
    }

    [HttpPost("client-logs")]
    [RequestSizeLimit(262_144)]
    [EnableRateLimiting("client-log-upload")]
    [ProducesResponseType(typeof(ClientLogUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadClientLogs([FromBody] ClientLogUploadRequest? request)
    {
        if (request?.Entries == null)
        {
            return BadRequest(new { error = "Request and entries collection must not be null." });
        }

        Guid? userId = null;
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var parsedId))
        {
            userId = parsedId;
        }

        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirst("email")?.Value;

        try
        {
            var result = await clientLogService.IngestAsync(userId, email, request, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("client-logs")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(IReadOnlyList<ClientLogViewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClientLogs(
        [FromQuery] int hours = 24,
        [FromQuery] string? deviceId = null,
        [FromQuery] string? minLevel = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] int limit = 500)
    {
        var logs = await clientLogService.QueryAsync(hours, deviceId, minLevel, correlationId, limit, HttpContext.RequestAborted);
        return Ok(logs);
    }

    [HttpGet("log")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(IReadOnlyList<SyncAuditEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSyncLog(
        [FromQuery] int hours = 72,
        [FromQuery] int limit = 200,
        [FromQuery] bool failedOnly = false)
    {
        var log = await syncService.GetRecentSyncAuditAsync(hours, limit, failedOnly, HttpContext.RequestAborted);
        return Ok(log);
    }
}

