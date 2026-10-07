using ClinicManagementApp.Api.Domain.Common;

namespace ClinicManagementApp.Api.Domain.Entities;

/// <summary>
/// Domain entity representing operational clinic expenses in PostgreSQL.
/// </summary>
public class ExpenseEntity : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Category { get; set; } = "Utilities";
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public bool IsRecurringFixedCost { get; set; }
}

