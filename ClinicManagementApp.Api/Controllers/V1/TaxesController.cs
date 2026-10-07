using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Reports;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagementApp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/taxes")]
[Authorize]
public class TaxesController(ITaxCalculationService taxService) : ControllerBase
{
    [HttpGet("quarterly")]
    [ProducesResponseType(typeof(ApiResponse<TaxCalculationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQuarterlyTax(
        [FromQuery] int year = 2026,
        [FromQuery] int quarter = 1,
        [FromQuery] string regime = "EightPercentFlat",
        [FromQuery] string deductionMethod = "Itemized",
        [FromQuery] decimal form2307Credits = 0m)
    {
        var result = await taxService.CalculateQuarterlyTaxAsync(year, quarter, regime, deductionMethod, form2307Credits);
        return Ok(result);
    }

    [HttpGet("deadlines")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TaxDeadlineAlertDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeadlines()
    {
        var result = await taxService.GetUpcomingDeadlinesAsync();
        return Ok(result);
    }

    [HttpGet("senior-pwd-discount")]
    [ProducesResponseType(typeof(ApiResponse<SeniorPwdDiscountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SeniorPwdDiscountDto>), StatusCodes.Status400BadRequest)]
    public IActionResult CalculateSeniorDiscount([FromQuery] decimal grossFee, [FromQuery] bool isVatRegistered = false)
    {
        var result = taxService.CalculateSeniorPwdDiscount(grossFee, isVatRegistered);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

