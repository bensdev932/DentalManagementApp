using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Appointments;

namespace ClinicManagementApp.Api.Services.Interfaces;

/// <summary>
/// Domain service contract for appointment scheduling, status transitions, and reminder automation.
/// </summary>
public interface IAppointmentService
{
    Task<ApiResponse<IReadOnlyList<AppointmentDto>>> GetAppointmentsAsync(AppointmentFilterParams filter);
    Task<ApiResponse<IReadOnlyList<AppointmentDto>>> GetUpcomingAppointmentsAsync(int limit);
    Task<ApiResponse<IReadOnlyList<AppointmentDto>>> GetPatientAppointmentsAsync(int patientId);
    Task<ApiResponse<AppointmentDto>> ScheduleAppointmentAsync(CreateAppointmentRequest request);
    Task<ApiResponse<AppointmentDto>> RescheduleAppointmentAsync(int id, RescheduleAppointmentRequest request);
    Task<ApiResponse> CancelAppointmentAsync(int id);
    Task<ApiResponse<CompleteAppointmentResponse>> CompleteAppointmentAsync(int id);
    Task<ApiResponse> TriggerSmsReminderAsync(int appointmentId);
}

