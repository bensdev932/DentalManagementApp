using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Orthodontics;

namespace ClinicManagementApp.Api.Services.Interfaces;

/// <summary>
/// Domain service contract for orthodontic contracts, installment calculations, and amortization tracking.
/// </summary>
public interface IOrthodonticsService
{
    Task<ApiResponse<OrthodonticsSummaryDto>> GetPatientSummaryAsync(int patientId);
    Task<ApiResponse<OrthodonticContractDto>> CreateContractAsync(CreateContractRequest request);
    Task<ApiResponse<OrthodonticPaymentDto>> RecordPaymentAsync(RecordPaymentRequest request);
    Task<ApiResponse<IReadOnlyList<OrthodonticPaymentDto>>> GetPatientPaymentsAsync(int patientId);
}

