namespace ClinicManagementApp.Api.Domain.Entities;

/// <summary>
/// Domain entity representing asynchronous patient creation tasks processed via the PostgreSQL Transactional Outbox pattern.
/// </summary>
public class PatientOutboxTaskEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PayloadJson { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public int RetryCount { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAtUtc { get; set; }
}
