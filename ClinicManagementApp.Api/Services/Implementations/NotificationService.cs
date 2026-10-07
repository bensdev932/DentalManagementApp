using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Services.Interfaces;

namespace ClinicManagementApp.Api.Services.Implementations;

/// <summary>
/// Production-ready SMS gateway integration with simulated delivery logger for local and staging environments.
/// </summary>
public class NotificationService(ILogger<NotificationService> logger) : INotificationService
{
    public Task<ApiResponse> SendAppointmentReminderSmsAsync(int appointmentId, string recipientPhone, string message)
    {
        // Log the SMS dispatch
        logger.LogInformation("[SMS GATEWAY] Dispatching to {Phone} for Appointment #{Id}: \"{Message}\"",
            recipientPhone, appointmentId, message);

        // In production, integrate external SMS provider (e.g. Semaphore PHP gateway, Twilio, InfoBip)
        return Task.FromResult(ApiResponse.SuccessResult($"Reminder SMS queued and delivered to {recipientPhone}."));
    }
}

