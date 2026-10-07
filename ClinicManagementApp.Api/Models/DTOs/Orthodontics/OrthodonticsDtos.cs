namespace ClinicManagementApp.Api.Models.DTOs.Orthodontics;

public record OrthodonticsSummaryDto(
    decimal TotalContractPrice,
    decimal DownPayment,
    decimal MonthlyAmortization,
    decimal TotalPaid,
    decimal RemainingBalance,
    int TermsMonths,
    int TermsRemaining,
    double PaymentProgress,
    bool HasContract,
    bool IsActive,
    bool IsFullyPaid
);

public record CreateContractRequest(
    int PatientId,
    decimal TotalContractPrice,
    decimal DownPayment,
    int TermsMonths,
    DateTime? StartDate
);

public record OrthodonticContractDto(
    int Id,
    int PatientId,
    decimal TotalContractPrice,
    decimal DownPayment,
    int TermsMonths,
    DateTime StartDate,
    bool IsActive,
    DateTime CreatedAtUtc
);

public record RecordPaymentRequest(
    int PatientId,
    decimal Amount,
    DateTime? PaymentDate,
    string? Notes
);

public record OrthodonticPaymentDto(
    int Id,
    int ContractId,
    int PatientId,
    decimal Amount,
    DateTime PaymentDate,
    string Notes,
    DateTime CreatedAtUtc
);

