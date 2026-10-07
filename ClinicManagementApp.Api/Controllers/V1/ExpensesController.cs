using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Expenses;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagementApp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/expenses")]
[Authorize]
public class ExpensesController(IExpenseService expenseService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExpenseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenses([FromQuery] ExpenseFilterParams filter)
    {
        var result = await expenseService.GetExpensesAsync(filter);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExpenseById(int id)
    {
        var result = await expenseService.GetExpenseByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request)
    {
        var result = await expenseService.CreateExpenseAsync(request);
        return result.Success
            ? CreatedAtAction(nameof(GetExpenseById), new { id = result.Data!.Id }, result)
            : BadRequest(result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateExpense(int id, [FromBody] UpdateExpenseRequest request)
    {
        var result = await expenseService.UpdateExpenseAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        var result = await expenseService.DeleteExpenseAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExpenseCategorySummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategorySummary([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var result = await expenseService.GetCategoryBreakdownAsync(startDate, endDate);
        return Ok(result);
    }
}

