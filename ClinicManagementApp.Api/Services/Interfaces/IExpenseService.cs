using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Expenses;

namespace ClinicManagementApp.Api.Services.Interfaces;

/// <summary>
/// Domain service contract for managing clinic expenses and overhead categorizations.
/// </summary>
public interface IExpenseService
{
    Task<ApiResponse<IReadOnlyList<ExpenseDto>>> GetExpensesAsync(ExpenseFilterParams filter);
    Task<ApiResponse<ExpenseDto>> GetExpenseByIdAsync(int id);
    Task<ApiResponse<ExpenseDto>> CreateExpenseAsync(CreateExpenseRequest request);
    Task<ApiResponse<ExpenseDto>> UpdateExpenseAsync(int id, UpdateExpenseRequest request);
    Task<ApiResponse> DeleteExpenseAsync(int id);
    Task<ApiResponse<IReadOnlyList<ExpenseCategorySummaryDto>>> GetCategoryBreakdownAsync(DateTime? startDate, DateTime? endDate);
}

