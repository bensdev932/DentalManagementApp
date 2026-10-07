using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Treatments;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagementApp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/treatments")]
[Authorize]
public class TreatmentsController(ITreatmentService treatmentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DentalTreatmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTreatments([FromQuery] TreatmentFilterParams filter)
    {
        var result = await treatmentService.GetTreatmentsAsync(filter);
        return Ok(result);
    }

    [HttpGet("patient/{patientId:int}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DentalTreatmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientTreatments(int patientId)
    {
        var result = await treatmentService.GetPatientTreatmentsAsync(patientId);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DentalTreatmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<DentalTreatmentDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTreatment([FromBody] CreateTreatmentRequest request)
    {
        var result = await treatmentService.CreateTreatmentAsync(request);
        return result.Success ? StatusCode(StatusCodes.Status201Created, result) : BadRequest(result);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteTreatment(int id)
    {
        var result = await treatmentService.DeleteTreatmentAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

