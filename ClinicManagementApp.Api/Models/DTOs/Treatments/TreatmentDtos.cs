namespace ClinicManagementApp.Api.Models.DTOs.Treatments;

public record DentalTreatmentDto(
    int Id,
    int PatientId,
    string TreatmentType,
    decimal ConsultationFee,
    DateTime DateCreated,
    bool IsInstallment,
    decimal TotalContractPrice,
    decimal DownPayment,
    int TermsMonths,
    DateTime CreatedAtUtc
);

public record CreateTreatmentRequest(
    int PatientId,
    string TreatmentType,
    decimal ConsultationFee,
    DateTime? DateCreated,
    bool IsInstallment,
    decimal? TotalContractPrice,
    decimal? DownPayment,
    int? TermsMonths
);

public class TreatmentFilterParams
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public record ProcedureCatalogDto(
    int Id,
    string Name,
    string Category,
    decimal BaselinePrice,
    string Description,
    bool IsActive
);

public record CreateProcedureRequest(
    string Name,
    string? Category,
    decimal BaselinePrice,
    string? Description
);

