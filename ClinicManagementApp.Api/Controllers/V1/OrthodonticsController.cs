using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Orthodontics;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagementApp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/orthodontics")]
[Authorize]
public class OrthodonticsController(IOrthodonticsService orthodonticsService) : ControllerBase
{
    [HttpGet("patient/{patientId:int}/summary")]
    [ProducesResponseType(typeof(ApiResponse<OrthodonticsSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(int patientId)
    {
        var result = await orthodonticsService.GetPatientSummaryAsync(patientId);
        return Ok(result);
    }

    [HttpPost("contract")]
    [ProducesResponseType(typeof(ApiResponse<OrthodonticContractDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<OrthodonticContractDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateContract([FromBody] CreateContractRequest request)
    {
        var result = await orthodonticsService.CreateContractAsync(request);
        return result.Success ? StatusCode(StatusCodes.Status201Created, result) : BadRequest(result);
    }

    [HttpPost("payment")]
    [ProducesResponseType(typeof(ApiResponse<OrthodonticPaymentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<OrthodonticPaymentDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordPayment([FromBody] RecordPaymentRequest request)
    {
        var result = await orthodonticsService.RecordPaymentAsync(request);
        return result.Success ? StatusCode(StatusCodes.Status201Created, result) : BadRequest(result);
    }

    [HttpGet("patient/{patientId:int}/payments")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OrthodonticPaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayments(int patientId)
    {
        var result = await orthodonticsService.GetPatientPaymentsAsync(patientId);
        return Ok(result);
    }
}

