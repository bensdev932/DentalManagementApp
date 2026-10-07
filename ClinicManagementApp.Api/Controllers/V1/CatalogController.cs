using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Treatments;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagementApp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/catalog")]
[Authorize]
public class CatalogController(ITreatmentService treatmentService) : ControllerBase
{
    [HttpGet("procedures")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProcedureCatalogDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProcedures()
    {
        var result = await treatmentService.GetProcedureCatalogAsync();
        return Ok(result);
    }

    [HttpPost("procedures")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse<ProcedureCatalogDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ProcedureCatalogDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddProcedure([FromBody] CreateProcedureRequest request)
    {
        var result = await treatmentService.AddProcedureToCatalogAsync(request);
        return result.Success ? StatusCode(StatusCodes.Status201Created, result) : BadRequest(result);
    }
}

