using System.Text.Json;
using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Appointments;
using ClinicManagementApp.Api.Models.DTOs.Expenses;
using ClinicManagementApp.Api.Models.DTOs.Patients;
using ClinicManagementApp.Api.Models.DTOs.Sync;
using ClinicManagementApp.Api.Models.DTOs.Treatments;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementApp.Api.Services.Implementations;

/// <summary>
/// Implements idempotent offline-first sync engine matching mobile OutboxRecord architecture.
/// </summary>
public class SyncService(
    ApplicationDbContext context,
    IPatientService patientService,
    IAppointmentService appointmentService,
    ITreatmentService treatmentService,
    IExpenseService expenseService,
    ILogger<SyncService> logger) : ISyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ApiResponse<SyncPushResponse>> ProcessPushRecordAsync(SyncPushRequest request)
    {
        // 1. Idempotency Check
        var existingSync = await context.OutboxSyncRecords.FindAsync(request.Id);
        if (existingSync != null && existingSync.IsSuccess)
        {
            logger.LogInformation("Idempotent replay detected for sync record {Id}. Skipping execution.", request.Id);
            return ApiResponse<SyncPushResponse>.SuccessResult(
                new SyncPushResponse(true, existingSync.EntityId, "Already processed idempotently."));
        }

        string serverEntityId = request.EntityId;
        bool isSuccess = false;
        string? error = null;

        try
        {
            var entityType = request.EntityType.Trim().ToLower();
            switch (entityType)
            {
                case "patient":
                    serverEntityId = await HandlePatientMutationAsync(request);
                    break;

                case "appointment":
                    serverEntityId = await HandleAppointmentMutationAsync(request);
                    break;

                case "dentaltreatment" or "treatment":
                    serverEntityId = await HandleTreatmentMutationAsync(request);
                    break;

                case "expense":
                    serverEntityId = await HandleExpenseMutationAsync(request);
                    break;

                default:
                    throw new NotSupportedException($"Unsupported sync entity type: {request.EntityType}");
            }

            isSuccess = true;
            logger.LogInformation("Applied sync {Id} ({EntityType} #{EntityId}, op {Op}) → server id {ServerId}",
                request.Id, request.EntityType, request.EntityId, request.Operation, serverEntityId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply sync mutation for record {Id} ({EntityType} #{EntityId})",
                request.Id, request.EntityType, request.EntityId);
            error = ex.Message;
        }

        // Record or update the sync audit record for idempotency
        if (existingSync == null)
        {
            context.OutboxSyncRecords.Add(new OutboxSyncRecordEntity
            {
                Id = request.Id,
                EntityType = request.EntityType,
                EntityId = serverEntityId,
                Operation = request.Operation,
                PayloadJson = request.PayloadJson,
                CreatedAtUtc = request.CreatedAtUtc,
                ProcessedAtUtc = DateTime.UtcNow,
                IsSuccess = isSuccess,
                ErrorMessage = error
            });
        }
        else
        {
            existingSync.IsSuccess = isSuccess;
            existingSync.EntityId = serverEntityId;
            existingSync.ProcessedAtUtc = DateTime.UtcNow;
            existingSync.ErrorMessage = error;
        }

        await context.SaveChangesAsync();

        return isSuccess
            ? ApiResponse<SyncPushResponse>.SuccessResult(new SyncPushResponse(true, serverEntityId, "Processed successfully."))
            : ApiResponse<SyncPushResponse>.FailureResult(error ?? "Sync operation failed.", error ?? "Unknown error");
    }

    public async Task<ApiResponse<IReadOnlyList<SyncBatchItemResult>>> ProcessBatchAsync(IReadOnlyList<SyncPushRequest> records)
    {
        var results = new List<SyncBatchItemResult>();

        foreach (var record in records)
        {
            var result = await ProcessPushRecordAsync(record);
            results.Add(new SyncBatchItemResult(
                record.Id,
                result.Success,
                result.Success ? null : result.Message,
                result.Data?.ServerEntityId
            ));
        }

        return ApiResponse<IReadOnlyList<SyncBatchItemResult>>.SuccessResult(results);
    }

    public async Task<ApiResponse<SyncPullResponse>> PullDeltaAsync(DateTime? sinceUtc)
    {
        var threshold = sinceUtc.HasValue
            ? (sinceUtc.Value.Kind == DateTimeKind.Utc ? sinceUtc.Value : DateTime.SpecifyKind(sinceUtc.Value, DateTimeKind.Utc))
            : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

        // Fetch patients modified since threshold
        var patients = await context.Patients
            .AsNoTracking()
            .IgnoreQueryFilters() // Include deleted records so client can remove them locally
            .Where(p => p.CreatedAtUtc >= threshold || (p.UpdatedAtUtc.HasValue && p.UpdatedAtUtc.Value >= threshold) || (p.DeletedAtUtc.HasValue && p.DeletedAtUtc.Value >= threshold))
            .Select(p => new PatientDto(
                p.Id, p.FirstName, p.LastName, $"{p.FirstName} {p.LastName}".Trim(),
                p.Age, p.Gender, p.Phone, p.Role, p.ConsultationFee, p.ConsultationNotes,
                p.IsDeleted ? "Archived" : p.Status, p.CreatedAtUtc, p.UpdatedAtUtc))
            .ToListAsync();

        // Fetch appointments modified since threshold
        var appointments = await context.Appointments
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.CreatedAtUtc >= threshold || (a.UpdatedAtUtc.HasValue && a.UpdatedAtUtc.Value >= threshold) || (a.DeletedAtUtc.HasValue && a.DeletedAtUtc.Value >= threshold))
            .Select(a => new AppointmentDto(
                a.Id, a.PatientId, a.PatientName, a.PatientPhone, a.Procedure,
                a.AppointmentDateTime, a.DurationMinutes, a.IsDeleted ? "Cancelled" : a.Status,
                a.Notes, a.ReminderMinutesBefore, a.IsReminderScheduled, a.ReminderSentAtUtc, a.CreatedAtUtc))
            .ToListAsync();

        // Fetch treatments modified since threshold
        var treatments = await context.DentalTreatments
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.CreatedAtUtc >= threshold || (t.UpdatedAtUtc.HasValue && t.UpdatedAtUtc.Value >= threshold) || (t.DeletedAtUtc.HasValue && t.DeletedAtUtc.Value >= threshold))
            .Select(t => new DentalTreatmentDto(
                t.Id, t.PatientId, t.TreatmentType, t.ConsultationFee, t.DateCreated,
                t.IsInstallment, t.TotalContractPrice, t.DownPayment, t.TermsMonths, t.CreatedAtUtc))
            .ToListAsync();

        // Fetch expenses modified since threshold
        var expenses = await context.Expenses
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.CreatedAtUtc >= threshold || (e.UpdatedAtUtc.HasValue && e.UpdatedAtUtc.Value >= threshold) || (e.DeletedAtUtc.HasValue && e.DeletedAtUtc.Value >= threshold))
            .Select(e => new ExpenseDto(
                e.Id, e.Title, e.Amount, e.Category, e.Date, e.IsRecurringFixedCost, e.CreatedAtUtc))
            .ToListAsync();

        var response = new SyncPullResponse(
            patients,
            appointments,
            treatments,
            expenses,
            DateTime.UtcNow
        );

        return ApiResponse<SyncPullResponse>.SuccessResult(response);
    }

    private async Task<string> HandlePatientMutationAsync(SyncPushRequest request)
    {
        // Operation: 1=Insert, 2=Update, 3=Delete
        if (request.Operation == 1)
        {
            var createDto = JsonSerializer.Deserialize<CreatePatientRequest>(request.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Invalid patient create payload.");
            var res = await patientService.CreatePatientAsync(createDto);
            if (!res.Success) throw new InvalidOperationException(res.Message);
            return res.Data!.Id.ToString();
        }
        else if (request.Operation == 2)
        {
            if (!int.TryParse(request.EntityId, out var id)) throw new ArgumentException("Invalid entity ID");
            var updateDto = JsonSerializer.Deserialize<UpdatePatientRequest>(request.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Invalid patient update payload.");
            var res = await patientService.UpdatePatientAsync(id, updateDto);
            if (!res.Success) throw new InvalidOperationException(res.Message);
            return id.ToString();
        }
        else if (request.Operation == 3)
        {
            if (!int.TryParse(request.EntityId, out var id)) throw new ArgumentException("Invalid entity ID");
            var res = await patientService.DeletePatientAsync(id);
            if (!res.Success) throw new InvalidOperationException(res.Message);
            return id.ToString();
        }

        throw new NotSupportedException($"Unsupported patient operation: {request.Operation}");
    }

    private async Task<string> HandleAppointmentMutationAsync(SyncPushRequest request)
    {
        if (request.Operation == 1)
        {
            var createDto = JsonSerializer.Deserialize<CreateAppointmentRequest>(request.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Invalid appointment create payload.");
            var res = await appointmentService.ScheduleAppointmentAsync(createDto);
            if (!res.Success) throw new InvalidOperationException(res.Message);
            return res.Data!.Id.ToString();
        }
        else if (request.Operation == 2)
        {
            if (!int.TryParse(request.EntityId, out var id)) throw new ArgumentException("Invalid entity ID");
            var reschedDto = JsonSerializer.Deserialize<RescheduleAppointmentRequest>(request.PayloadJson, JsonOptions);
            if (reschedDto != null)
            {
                var res = await appointmentService.RescheduleAppointmentAsync(id, reschedDto);
                if (!res.Success) throw new InvalidOperationException(res.Message);
            }
            return id.ToString();
        }
        else if (request.Operation == 3)
        {
            if (!int.TryParse(request.EntityId, out var id)) throw new ArgumentException("Invalid entity ID");
            var res = await appointmentService.CancelAppointmentAsync(id);
            if (!res.Success) throw new InvalidOperationException(res.Message);
            return id.ToString();
        }

        throw new NotSupportedException($"Unsupported appointment operation: {request.Operation}");
    }

    private async Task<string> HandleTreatmentMutationAsync(SyncPushRequest request)
    {
        if (request.Operation == 1)
        {
            var createDto = JsonSerializer.Deserialize<CreateTreatmentRequest>(request.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Invalid treatment create payload.");
            var res = await treatmentService.CreateTreatmentAsync(createDto);
            if (!res.Success) throw new InvalidOperationException(res.Message);
            return res.Data!.Id.ToString();
        }
        else if (request.Operation == 3)
        {
            if (!int.TryParse(request.EntityId, out var id)) throw new ArgumentException("Invalid entity ID");
            var res = await treatmentService.DeleteTreatmentAsync(id);
            if (!res.Success) throw new InvalidOperationException(res.Message);
            return id.ToString();
        }

        throw new NotSupportedException($"Unsupported treatment operation: {request.Operation}");
    }

    private async Task<string> HandleExpenseMutationAsync(SyncPushRequest request)
    {
        if (request.Operation == 1)
        {
            var createDto = JsonSerializer.Deserialize<CreateExpenseRequest>(request.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Invalid expense create payload.");
            var res = await expenseService.CreateExpenseAsync(createDto);
            if (!res.Success) throw new InvalidOperationException(res.Message);
            return res.Data!.Id.ToString();
        }
        else if (request.Operation == 2)
        {
            if (!int.TryParse(request.EntityId, out var id)) throw new ArgumentException("Invalid entity ID");
            var updateDto = JsonSerializer.Deserialize<UpdateExpenseRequest>(request.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException("Invalid expense update payload.");
            var res = await expenseService.UpdateExpenseAsync(id, updateDto);
            if (!res.Success) throw new InvalidOperationException(res.Message);
            return id.ToString();
        }
        else if (request.Operation == 3)
        {
            if (!int.TryParse(request.EntityId, out var id)) throw new ArgumentException("Invalid entity ID");
            var res = await expenseService.DeleteExpenseAsync(id);
            if (!res.Success) throw new InvalidOperationException(res.Message);
            return id.ToString();
        }

        throw new NotSupportedException($"Unsupported expense operation: {request.Operation}");
    }

    public async Task<IReadOnlyList<SyncAuditEntryDto>> GetRecentSyncAuditAsync(
        int hours = 72,
        int limit = 200,
        bool failedOnly = false,
        CancellationToken ct = default)
    {
        if (hours < 1) hours = 1;
        if (hours > 336) hours = 336;

        if (limit < 1) limit = 1;
        if (limit > 500) limit = 500;

        var cutoff = DateTime.UtcNow.AddHours(-hours);
        var query = context.OutboxSyncRecords
            .AsNoTracking()
            .Where(r => r.ProcessedAtUtc >= cutoff);

        if (failedOnly)
        {
            query = query.Where(r => !r.IsSuccess);
        }

        return await query
            .OrderBy(r => r.IsSuccess)
            .ThenByDescending(r => r.ProcessedAtUtc)
            .Take(limit)
            .Select(r => new SyncAuditEntryDto(
                r.Id,
                r.EntityType,
                r.EntityId,
                r.Operation,
                r.CreatedAtUtc,
                r.ProcessedAtUtc,
                r.IsSuccess,
                r.ErrorMessage
            ))
            .ToListAsync(ct);
    }
}
