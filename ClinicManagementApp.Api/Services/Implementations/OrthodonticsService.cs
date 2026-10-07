using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.ValueObjects;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Orthodontics;
using ClinicManagementApp.Api.Services.Implementations.Orthodontics;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClinicManagementApp.Api.Services.Implementations;

/// <summary>
/// Facade service orchestrating orthodontic operations by delegating to focused,
/// single-responsibility handlers. Preserves API backwards compatibility.
/// </summary>
public class OrthodonticsService(
    GetOrthodonticSummaryHandler summaryHandler,
    CreateOrthodonticContractHandler createContractHandler,
    RecordOrthodonticPaymentHandler recordPaymentHandler,
    GetOrthodonticPaymentsHandler paymentsHandler) : IOrthodonticsService
{
    /// <summary>
    /// Convenience constructor for testing and backwards compatibility with existing callers.
    /// </summary>
    public OrthodonticsService(ApplicationDbContext context, ILogger<OrthodonticsService> logger)
        : this(
            new GetOrthodonticSummaryHandler(context),
            new CreateOrthodonticContractHandler(context, NullLogger<CreateOrthodonticContractHandler>.Instance),
            new RecordOrthodonticPaymentHandler(context, NullLogger<RecordOrthodonticPaymentHandler>.Instance),
            new GetOrthodonticPaymentsHandler(context))
    {
    }

    public Task<ApiResponse<OrthodonticsSummaryDto>> GetPatientSummaryAsync(int patientId)
    {
        if (patientId <= 0)
        {
            return Task.FromResult(ApiResponse<OrthodonticsSummaryDto>.FailureResult("Patient ID must be greater than zero."));
        }

        return summaryHandler.HandleAsync(new PatientId(patientId));
    }

    public Task<ApiResponse<OrthodonticContractDto>> CreateContractAsync(CreateContractRequest request)
    {
        return createContractHandler.HandleAsync(request);
    }

    public Task<ApiResponse<OrthodonticPaymentDto>> RecordPaymentAsync(RecordPaymentRequest request)
    {
        return recordPaymentHandler.HandleAsync(request);
    }

    public Task<ApiResponse<IReadOnlyList<OrthodonticPaymentDto>>> GetPatientPaymentsAsync(int patientId)
    {
        if (patientId <= 0)
        {
            return Task.FromResult(ApiResponse<IReadOnlyList<OrthodonticPaymentDto>>.FailureResult("Patient ID must be greater than zero."));
        }

        return paymentsHandler.HandleAsync(new PatientId(patientId));
    }
}

