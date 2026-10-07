namespace ClinicManagementApp.Api.Models.DTOs.Appointments;

public record AppointmentDto(
    int Id,
    int PatientId,
    string PatientName,
    string PatientPhone,
    string Procedure,
    DateTime AppointmentDateTime,
    int DurationMinutes,
    string Status,
    string Notes,
    int ReminderMinutesBefore,
    bool IsReminderScheduled,
    DateTime? ReminderSentAtUtc,
    DateTime CreatedAtUtc
);

public record CreateAppointmentRequest(
    int? PatientId,
    string PatientName,
    string PatientPhone,
    string Procedure,
    DateTime AppointmentDateTime,
    int DurationMinutes,
    string? Notes,
    int ReminderMinutesBefore
);

public record RescheduleAppointmentRequest(
    DateTime NewDateTime,
    int DurationMinutes
);

public record CompleteAppointmentResponse(
    AppointmentDto Appointment,
    int GeneratedTreatmentId,
    decimal ConsultationFee
);

public record SendReminderRequest(
    int AppointmentId
);

public class AppointmentFilterParams
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Status { get; init; }
}

