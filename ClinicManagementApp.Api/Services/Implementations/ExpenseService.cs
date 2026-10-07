using ClinicManagementApp.Api.Common;
using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Expenses;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ClinicManagementApp.Api.Services.Implementations;

/// <summary>
/// Implements expense tracking, updates, soft deletion, and category aggregation with memory caching.
/// </summary>
public class ExpenseService(
    ApplicationDbContext context,
    IMemoryCache memoryCache,
    ILogger<ExpenseService> logger) : IExpenseService
{
    public async Task<ApiResponse<IReadOnlyList<ExpenseDto>>> GetExpensesAsync(ExpenseFilterParams filter)
    {
        var query = context.Expenses.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(e => e.Category.ToLower() == filter.Category.Trim().ToLower());
        }

        if (filter.StartDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(filter.StartDate.Value, DateTimeKind.Utc);
            query = query.Where(e => e.Date >= startUtc);
        }

        if (filter.EndDate.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(filter.EndDate.Value, DateTimeKind.Utc);
            query = query.Where(e => e.Date <= endUtc);
        }

        var expenses = await query
            .OrderByDescending(e => e.Date)
            .Select(e => MapToDto(e))
            .ToListAsync();

        return ApiResponse<IReadOnlyList<ExpenseDto>>.SuccessResult(expenses);
    }

    public async Task<ApiResponse<ExpenseDto>> GetExpenseByIdAsync(int id)
    {
        var expense = await context.Expenses.FindAsync(id);
        if (expense == null)
        {
            return ApiResponse<ExpenseDto>.FailureResult($"Expense with ID {id} was not found.");
        }

        return ApiResponse<ExpenseDto>.SuccessResult(MapToDto(expense));
    }

    public async Task<ApiResponse<ExpenseDto>> CreateExpenseAsync(CreateExpenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return ApiResponse<ExpenseDto>.FailureResult("Expense title is required.");
        }

        var expense = new ExpenseEntity
        {
            Title = request.Title.Trim(),
            Amount = request.Amount,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Utilities" : request.Category.Trim(),
            Date = request.Date ?? DateTime.UtcNow,
            IsRecurringFixedCost = request.IsRecurringFixedCost,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Expenses.Add(expense);
        await context.SaveChangesAsync();

        CacheKeys.InvalidateFinancialData(memoryCache);

        logger.LogInformation("Logged expense: #{Id} \"{Title}\" ₱{Amount}", expense.Id, expense.Title, expense.Amount);
        return ApiResponse<ExpenseDto>.SuccessResult(MapToDto(expense), "Expense logged successfully.");
    }

    public async Task<ApiResponse<ExpenseDto>> UpdateExpenseAsync(int id, UpdateExpenseRequest request)
    {
        var expense = await context.Expenses.FindAsync(id);
        if (expense == null)
        {
            return ApiResponse<ExpenseDto>.FailureResult($"Expense with ID {id} was not found.");
        }

        expense.Title = request.Title.Trim();
        expense.Amount = request.Amount;
        if (!string.IsNullOrWhiteSpace(request.Category)) expense.Category = request.Category.Trim();
        if (request.Date.HasValue) expense.Date = request.Date.Value;
        expense.IsRecurringFixedCost = request.IsRecurringFixedCost;
        expense.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();

        CacheKeys.InvalidateFinancialData(memoryCache);

        logger.LogInformation("Updated expense #{Id}", expense.Id);
        return ApiResponse<ExpenseDto>.SuccessResult(MapToDto(expense), "Expense updated successfully.");
    }

    public async Task<ApiResponse> DeleteExpenseAsync(int id)
    {
        var expense = await context.Expenses.FindAsync(id);
        if (expense == null)
        {
            return ApiResponse.FailureResult($"Expense with ID {id} was not found.");
        }

        expense.IsDeleted = true;
        expense.DeletedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();

        CacheKeys.InvalidateFinancialData(memoryCache);

        logger.LogInformation("Expense #{Id} soft-deleted", id);
        return ApiResponse.SuccessResult("Expense deleted successfully.");
    }

    public async Task<ApiResponse<IReadOnlyList<ExpenseCategorySummaryDto>>> GetCategoryBreakdownAsync(DateTime? startDate, DateTime? endDate)
    {
        var query = context.Expenses.AsNoTracking().AsQueryable();

        if (startDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
            query = query.Where(e => e.Date >= startUtc);
        }
        if (endDate.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc);
            query = query.Where(e => e.Date <= endUtc);
        }

        var rawBreakdown = await query
            .GroupBy(e => e.Category)
            .Select(g => new
            {
                Category = g.Key,
                TotalAmount = g.Sum(e => e.Amount),
                Count = g.Count()
            })
            .OrderByDescending(b => b.TotalAmount)
            .ToListAsync();

        var breakdown = rawBreakdown
            .Select(b => new ExpenseCategorySummaryDto(b.Category ?? "General", b.TotalAmount, b.Count))
            .ToList();

        return ApiResponse<IReadOnlyList<ExpenseCategorySummaryDto>>.SuccessResult(breakdown);
    }

    private static ExpenseDto MapToDto(ExpenseEntity e) =>
        new(
            e.Id,
            e.Title,
            e.Amount,
            e.Category,
            e.Date,
            e.IsRecurringFixedCost,
            e.CreatedAtUtc
        );
}

