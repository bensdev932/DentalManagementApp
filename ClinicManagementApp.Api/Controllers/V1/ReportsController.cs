using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Reports;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagementApp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportsController(IFinancialReportingService reportingService) : ControllerBase
{
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<DashboardFinancialSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await reportingService.GetDashboardSummaryAsync();
        return Ok(result);
    }

    [HttpGet("period")]
    [ProducesResponseType(typeof(ApiResponse<PeriodFinancialSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPeriod([FromQuery] string? period = "ThisMonth")
    {
        var result = await reportingService.GetPeriodSummaryAsync(period);
        return Ok(result);
    }
}

