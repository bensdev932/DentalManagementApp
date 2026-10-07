using ClinicManagementApp.Api.Models.Common;

namespace ClinicManagementApp.Api.Services.Interfaces;

/// <summary>
/// Abstraction for dispatching patient appointment reminders via SMS or WhatsApp.
/// Adheres to Dependency Inversion Principle (DIP).
/// </summary>
public interface INotificationService
{
    Task<ApiResponse> SendAppointmentReminderSmsAsync(int appointmentId, string recipientPhone, string message);
}

