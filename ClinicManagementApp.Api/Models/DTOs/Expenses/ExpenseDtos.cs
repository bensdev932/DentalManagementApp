namespace ClinicManagementApp.Api.Models.DTOs.Expenses;

public record ExpenseDto(
    int Id,
    string Title,
    decimal Amount,
    string Category,
    DateTime Date,
    bool IsRecurringFixedCost,
    DateTime CreatedAtUtc
);

public record CreateExpenseRequest(
    string Title,
    decimal Amount,
    string Category,
    DateTime? Date,
    bool IsRecurringFixedCost
);

public record UpdateExpenseRequest(
    string Title,
    decimal Amount,
    string Category,
    DateTime? Date,
    bool IsRecurringFixedCost
);

public class ExpenseFilterParams
{
    public string? Category { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public record ExpenseCategorySummaryDto(
    string Category,
    decimal TotalAmount,
    int Count
);

