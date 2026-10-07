using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Treatments;

namespace ClinicManagementApp.Api.Services.Interfaces;

/// <summary>
/// Domain service contract for clinical treatment procedures and procedure catalog pricing.
/// </summary>
public interface ITreatmentService
{
    Task<ApiResponse<IReadOnlyList<DentalTreatmentDto>>> GetTreatmentsAsync(TreatmentFilterParams filter);
    Task<ApiResponse<IReadOnlyList<DentalTreatmentDto>>> GetPatientTreatmentsAsync(int patientId);
    Task<ApiResponse<DentalTreatmentDto>> CreateTreatmentAsync(CreateTreatmentRequest request);
    Task<ApiResponse> DeleteTreatmentAsync(int id);
    Task<ApiResponse<IReadOnlyList<ProcedureCatalogDto>>> GetProcedureCatalogAsync();
    Task<ApiResponse<ProcedureCatalogDto>> AddProcedureToCatalogAsync(CreateProcedureRequest request);
}

