using ClinicManagementApp.Api.Models.Common;

namespace ClinicManagementApp.Api.Models.DTOs.Patients;

public record PatientDto(
    int Id,
    string FirstName,
    string LastName,
    string FullName,
    int Age,
    string Gender,
    string Phone,
    string Role,
    decimal ConsultationFee,
    string ConsultationNotes,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record PatientDetailDto(
    int Id,
    string FirstName,
    string LastName,
    string FullName,
    int Age,
    string Gender,
    string Phone,
    string Role,
    decimal ConsultationFee,
    string ConsultationNotes,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int TotalAppointments,
    int TotalTreatments
);

public record CreatePatientRequest(
    string FirstName,
    string LastName,
    int Age,
    string Gender,
    string Phone,
    string? Role,
    decimal ConsultationFee,
    string? ConsultationNotes,
    string? Status
);

public record UpdatePatientRequest(
    string FirstName,
    string LastName,
    int Age,
    string Gender,
    string Phone,
    string? Role,
    decimal ConsultationFee,
    string? ConsultationNotes,
    string? Status
);

public class PatientFilterParams : PaginationParams
{
    public string? Status { get; init; }
}

