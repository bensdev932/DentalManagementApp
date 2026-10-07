using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Domain.ValueObjects;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Orthodontics;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementApp.Api.Services.Implementations.Orthodontics;

/// <summary>
/// Single-responsibility handler for creating orthodontic contracts and ledger initialization.
/// Enforces Object Calisthenics: guard clauses, zero 'else' keywords, max 1 indentation level.
/// </summary>
public class CreateOrthodonticContractHandler(
    ApplicationDbContext context,
    ILogger<CreateOrthodonticContractHandler> logger)
{
    public async Task<ApiResponse<OrthodonticContractDto>> HandleAsync(CreateContractRequest request, CancellationToken ct = default)
    {
        var patientExists = await context.Patients.AnyAsync(p => p.Id == request.PatientId, ct);
        if (!patientExists)
        {
            return ApiResponse<OrthodonticContractDto>.FailureResult($"Patient with ID {request.PatientId} was not found.");
        }

        await DeactivateExistingContractsAsync(request.PatientId, ct);

        var contract = BuildContractEntity(request);
        context.OrthodonticContracts.Add(contract);

        TryAddDownPaymentTreatment(contract);

        await context.SaveChangesAsync(ct);
        logger.LogInformation("Orthodontic contract #{Id} created for Patient #{PatientId}", contract.Id, contract.PatientId);

        return ApiResponse<OrthodonticContractDto>.SuccessResult(MapToContractDto(contract), "Orthodontic contract created.");
    }

    private async Task DeactivateExistingContractsAsync(int patientId, CancellationToken ct)
    {
        var activeContracts = await context.OrthodonticContracts
            .Where(c => c.PatientId == patientId && c.IsActive)
            .ToListAsync(ct);

        foreach (var c in activeContracts)
        {
            c.IsActive = false;
            c.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private static OrthodonticContractEntity BuildContractEntity(CreateContractRequest request)
    {
        var totalContract = new Money(request.TotalContractPrice);
        var downPayment = new Money(request.DownPayment);
        var termsMonths = request.TermsMonths > 0 ? request.TermsMonths : 24;
        var startDate = request.StartDate.HasValue
            ? DateTime.SpecifyKind(request.StartDate.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        return new OrthodonticContractEntity
        {
            PatientId = request.PatientId,
            TotalContractPrice = totalContract.Amount,
            DownPayment = downPayment.Amount,
            TermsMonths = termsMonths,
            StartDate = startDate,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private void TryAddDownPaymentTreatment(OrthodonticContractEntity contract)
    {
        if (contract.DownPayment <= 0m)
        {
            return;
        }

        var treatment = new DentalTreatmentEntity
        {
            PatientId = contract.PatientId,
            TreatmentType = "Braces (Down Payment)",
            ConsultationFee = contract.DownPayment,
            DateCreated = contract.StartDate,
            IsInstallment = true,
            TotalContractPrice = contract.TotalContractPrice,
            DownPayment = contract.DownPayment,
            TermsMonths = contract.TermsMonths,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.DentalTreatments.Add(treatment);
    }

    private static OrthodonticContractDto MapToContractDto(OrthodonticContractEntity c) =>
        new(
            c.Id,
            c.PatientId,
            c.TotalContractPrice,
            c.DownPayment,
            c.TermsMonths,
            c.StartDate,
            c.IsActive,
            c.CreatedAtUtc
        );
}

