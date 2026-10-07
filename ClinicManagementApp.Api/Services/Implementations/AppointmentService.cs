using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Appointments;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementApp.Api.Services.Implementations;

/// <summary>
/// Implements appointment scheduling, automated ledger generation upon completion, and reminder dispatches.
/// </summary>
public class AppointmentService(
    ApplicationDbContext context,
    INotificationService notificationService,
    ILogger<AppointmentService> logger) : IAppointmentService
{
    public async Task<ApiResponse<IReadOnlyList<AppointmentDto>>> GetAppointmentsAsync(AppointmentFilterParams filter)
    {
        var query = context.Appointments.AsNoTracking().AsQueryable();

        if (filter.StartDate.HasValue)
        {
            query = query.Where(a => a.AppointmentDateTime >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            query = query.Where(a => a.AppointmentDateTime <= filter.EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(a => a.Status.ToLower() == filter.Status.Trim().ToLower());
        }

        var appointments = await query
            .OrderBy(a => a.AppointmentDateTime)
            .Select(a => MapToDto(a))
            .ToListAsync();

        return ApiResponse<IReadOnlyList<AppointmentDto>>.SuccessResult(appointments);
    }

    public async Task<ApiResponse<IReadOnlyList<AppointmentDto>>> GetUpcomingAppointmentsAsync(int limit)
    {
        var now = DateTime.UtcNow;
        var appointments = await context.Appointments
            .AsNoTracking()
            .Where(a => a.AppointmentDateTime >= now && a.Status == "Scheduled")
            .OrderBy(a => a.AppointmentDateTime)
            .Take(limit > 0 ? limit : 10)
            .Select(a => MapToDto(a))
            .ToListAsync();

        return ApiResponse<IReadOnlyList<AppointmentDto>>.SuccessResult(appointments);
    }

    public async Task<ApiResponse<IReadOnlyList<AppointmentDto>>> GetPatientAppointmentsAsync(int patientId)
    {
        var appointments = await context.Appointments
            .AsNoTracking()
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.AppointmentDateTime)
            .Select(a => MapToDto(a))
            .ToListAsync();

        return ApiResponse<IReadOnlyList<AppointmentDto>>.SuccessResult(appointments);
    }

    public async Task<ApiResponse<AppointmentDto>> ScheduleAppointmentAsync(CreateAppointmentRequest request)
    {
        int patientId;

        if (request.PatientId.HasValue && request.PatientId.Value > 0)
        {
            var existingPatient = await context.Patients.FindAsync(request.PatientId.Value);
            if (existingPatient == null)
            {
                return ApiResponse<AppointmentDto>.FailureResult($"Patient with ID {request.PatientId.Value} was not found.");
            }
            patientId = existingPatient.Id;
        }
        else
        {
            // Auto-link or auto-register patient
            var matchedPatient = await context.Patients
                .FirstOrDefaultAsync(p => p.Phone == request.PatientPhone.Trim());

            if (matchedPatient != null)
            {
                patientId = matchedPatient.Id;
            }
            else
            {
                // Auto-create new patient record
                var names = request.PatientName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var firstName = names.Length > 0 ? names[0] : request.PatientName;
                var lastName = names.Length > 1 ? names[1] : string.Empty;

                var newPatient = new PatientEntity
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Phone = request.PatientPhone.Trim(),
                    Role = request.Procedure,
                    ConsultationFee = 500.00m,
                    Status = "Active",
                    CreatedAtUtc = DateTime.UtcNow
                };

                context.Patients.Add(newPatient);
                await context.SaveChangesAsync();
                patientId = newPatient.Id;
                logger.LogInformation("Auto-registered patient during appointment booking: {Name} (ID: {Id})", request.PatientName, patientId);
            }
        }

        var appointment = new AppointmentEntity
        {
            PatientId = patientId,
            PatientName = request.PatientName.Trim(),
            PatientPhone = request.PatientPhone.Trim(),
            Procedure = request.Procedure.Trim(),
            AppointmentDateTime = DateTime.SpecifyKind(request.AppointmentDateTime, DateTimeKind.Utc),
            DurationMinutes = request.DurationMinutes > 0 ? request.DurationMinutes : 30,
            Status = "Scheduled",
            Notes = request.Notes?.Trim() ?? string.Empty,
            ReminderMinutesBefore = request.ReminderMinutesBefore > 0 ? request.ReminderMinutesBefore : 30,
            IsReminderScheduled = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        logger.LogInformation("Appointment scheduled: #{Id} for {Patient} on {Time}", appointment.Id, appointment.PatientName, appointment.AppointmentDateTime);
        return ApiResponse<AppointmentDto>.SuccessResult(MapToDto(appointment), "Appointment scheduled successfully.");
    }

    public async Task<ApiResponse<AppointmentDto>> RescheduleAppointmentAsync(int id, RescheduleAppointmentRequest request)
    {
        var appointment = await context.Appointments.FindAsync(id);
        if (appointment == null)
        {
            return ApiResponse<AppointmentDto>.FailureResult($"Appointment with ID {id} was not found.");
        }

        appointment.AppointmentDateTime = DateTime.SpecifyKind(request.NewDateTime, DateTimeKind.Utc);
        if (request.DurationMinutes > 0)
        {
            appointment.DurationMinutes = request.DurationMinutes;
        }
        appointment.Status = "Scheduled";
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();
        logger.LogInformation("Appointment #{Id} rescheduled to {Time}", appointment.Id, appointment.AppointmentDateTime);

        return ApiResponse<AppointmentDto>.SuccessResult(MapToDto(appointment), "Appointment rescheduled successfully.");
    }

    public async Task<ApiResponse> CancelAppointmentAsync(int id)
    {
        var appointment = await context.Appointments.FindAsync(id);
        if (appointment == null)
        {
            return ApiResponse.FailureResult($"Appointment with ID {id} was not found.");
        }

        appointment.Status = "Cancelled";
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();
        logger.LogInformation("Appointment #{Id} marked as Cancelled", id);

        return ApiResponse.SuccessResult("Appointment marked as cancelled.");
    }

    public async Task<ApiResponse<CompleteAppointmentResponse>> CompleteAppointmentAsync(int id)
    {
        var appointment = await context.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment == null)
        {
            return ApiResponse<CompleteAppointmentResponse>.FailureResult($"Appointment with ID {id} was not found.");
        }

        if (appointment.Status == "Completed")
        {
            return ApiResponse<CompleteAppointmentResponse>.FailureResult("This appointment has already been completed.");
        }

        var fee = appointment.Patient?.ConsultationFee ?? 500.00m;

        // 1. Mark Appointment as Completed
        appointment.Status = "Completed";
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        // 2. Automatically post treatment to patient ledger
        var treatment = new DentalTreatmentEntity
        {
            PatientId = appointment.PatientId,
            TreatmentType = appointment.Procedure,
            ConsultationFee = fee,
            DateCreated = DateTime.UtcNow,
            IsInstallment = false,
            TotalContractPrice = 0,
            DownPayment = 0,
            TermsMonths = 0,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.DentalTreatments.Add(treatment);
        await context.SaveChangesAsync();

        logger.LogInformation("Appointment #{AppointmentId} completed. Auto-posted Treatment #{TreatmentId} with fee ₱{Fee}",
            appointment.Id, treatment.Id, fee);

        var response = new CompleteAppointmentResponse(
            MapToDto(appointment),
            treatment.Id,
            fee
        );

        return ApiResponse<CompleteAppointmentResponse>.SuccessResult(response, "Appointment marked as completed and treatment posted to ledger.");
    }

    public async Task<ApiResponse> TriggerSmsReminderAsync(int appointmentId)
    {
        var appointment = await context.Appointments.FindAsync(appointmentId);
        if (appointment == null)
        {
            return ApiResponse.FailureResult($"Appointment with ID {appointmentId} was not found.");
        }

        var message = $"Hi {appointment.PatientName}, this is a reminder for your clinic appointment for {appointment.Procedure} scheduled on {appointment.AppointmentDateTime:MMM dd, yyyy at hh:mm tt}.";
        var result = await notificationService.SendAppointmentReminderSmsAsync(appointment.Id, appointment.PatientPhone, message);

        if (result.Success)
        {
            appointment.ReminderSentAtUtc = DateTime.UtcNow;
            appointment.UpdatedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        return result;
    }

    private static AppointmentDto MapToDto(AppointmentEntity a) =>
        new(
            a.Id,
            a.PatientId,
            a.PatientName,
            a.PatientPhone,
            a.Procedure,
            a.AppointmentDateTime,
            a.DurationMinutes,
            a.Status,
            a.Notes,
            a.ReminderMinutesBefore,
            a.IsReminderScheduled,
            a.ReminderSentAtUtc,
            a.CreatedAtUtc
        );
}

