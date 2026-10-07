namespace ClinicManagementApp.Api.Domain.Entities;

/// <summary>
/// Domain entity representing offline synchronization records sent from mobile clients.
/// Uses Guid primary key matching client OutboxRecord.Id to guarantee idempotent processing.
/// </summary>
public class OutboxSyncRecordEntity
{
    /// <summary>
    /// Matches client-side OutboxRecord.Id to prevent duplicate processing on retries.
    /// </summary>
    public Guid Id { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// 1 = Insert, 2 = Update, 3 = Delete
    /// </summary>
    public int Operation { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }
}

