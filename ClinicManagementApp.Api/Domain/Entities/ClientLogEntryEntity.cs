namespace ClinicManagementApp.Api.Domain.Entities;

public class ClientLogEntryEntity
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string? DeviceModel { get; set; }
    public string? Platform { get; set; }
    public string? AppVersion { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
}

