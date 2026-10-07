using ClinicManagementApp.Api.Domain.Common;

namespace ClinicManagementApp.Api.Domain.Entities;

/// <summary>
/// Domain entity representing scheduled clinic appointments and reminders in PostgreSQL.
/// </summary>
public class AppointmentEntity : BaseEntity
{
    public int PatientId { get; set; }
    public PatientEntity? Patient { get; set; }

    public string PatientName { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public string Procedure { get; set; } = "Oral Prophylaxis (Cleaning)";
    public DateTime AppointmentDateTime { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public string Status { get; set; } = "Scheduled";
    public string Notes { get; set; } = string.Empty;
    public int ReminderMinutesBefore { get; set; } = 30;
    public bool IsReminderScheduled { get; set; } = true;
    public DateTime? ReminderSentAtUtc { get; set; }
}

