using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Patients;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagementApp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/patients")]
[Authorize]
public class PatientsController(IPatientService patientService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PatientDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatients([FromQuery] PatientFilterParams filter)
    {
        var result = await patientService.GetPatientsAsync(filter);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<PatientDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PatientDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientById(int id)
    {
        var result = await patientService.GetPatientByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PatientDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<PatientDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePatient([FromBody] CreatePatientRequest request)
    {
        var result = await patientService.EnqueuePatientCreationAsync(request);
        return result.Success
            ? Accepted(result)
            : BadRequest(result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<PatientDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PatientDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PatientDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePatient(int id, [FromBody] UpdatePatientRequest request)
    {
        var result = await patientService.UpdatePatientAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePatient(int id)
    {
        var result = await patientService.DeletePatientAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

