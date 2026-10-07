using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Patients;

namespace ClinicManagementApp.Api.Services.Interfaces;

/// <summary>
/// Domain service contract for patient management and clinical records.
/// Adheres to Interface Segregation Principle (ISP).
/// </summary>
public interface IPatientService
{
    Task<ApiResponse<PagedResult<PatientDto>>> GetPatientsAsync(PatientFilterParams filter);
    Task<ApiResponse<PatientDetailDto>> GetPatientByIdAsync(int id);
    Task<ApiResponse<PatientDto>> CreatePatientAsync(CreatePatientRequest request);
    Task<ApiResponse<PatientDto>> EnqueuePatientCreationAsync(CreatePatientRequest request);
    Task<ApiResponse<PatientDto>> UpdatePatientAsync(int id, UpdatePatientRequest request);
    Task<ApiResponse> DeletePatientAsync(int id);
}

