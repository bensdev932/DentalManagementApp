using ClinicManagementApp.Api.Models.DTOs.Appointments;
using ClinicManagementApp.Api.Models.DTOs.Expenses;
using ClinicManagementApp.Api.Models.DTOs.Patients;
using ClinicManagementApp.Api.Models.DTOs.Treatments;

namespace ClinicManagementApp.Api.Models.DTOs.Sync;

public record SyncPushRequest(
    Guid Id,
    string EntityType,
    string EntityId,
    int Operation, // 1 = Insert, 2 = Update, 3 = Delete
    string PayloadJson,
    DateTime CreatedAtUtc
);

public record SyncPushResponse(
    bool Success,
    string ServerEntityId,
    string? Message
);

public record SyncBatchItemResult(
    Guid RecordId,
    bool IsSuccess,
    string? Error,
    string? ServerEntityId
);

public record SyncPullResponse(
    IReadOnlyList<PatientDto> ModifiedPatients,
    IReadOnlyList<AppointmentDto> ModifiedAppointments,
    IReadOnlyList<DentalTreatmentDto> ModifiedTreatments,
    IReadOnlyList<ExpenseDto> ModifiedExpenses,
    DateTime SyncTimestampUtc
);

public record ClientLogEntryDto(
    Guid Id,
    DateTime TimestampUtc,
    string Level,
    string Category,
    string Message,
    string? Exception,
    string? CorrelationId
);

public record ClientLogUploadRequest(
    string DeviceId,
    string? DeviceModel,
    string? Platform,
    string? AppVersion,
    IReadOnlyList<ClientLogEntryDto> Entries
);

public record ClientLogUploadResponse(
    int Accepted,
    int Duplicates
);

public record ClientLogViewDto(
    Guid Id,
    DateTime TimestampUtc,
    DateTime ReceivedAtUtc,
    string? UserEmail,
    string DeviceId,
    string? DeviceModel,
    string? Platform,
    string? AppVersion,
    string Level,
    string Category,
    string Message,
    string? Exception,
    string? CorrelationId
);

public record SyncAuditEntryDto(
    Guid Id,
    string EntityType,
    string EntityId,
    int Operation,
    DateTime CreatedAtUtc,
    DateTime ProcessedAtUtc,
    bool IsSuccess,
    string? ErrorMessage
);

