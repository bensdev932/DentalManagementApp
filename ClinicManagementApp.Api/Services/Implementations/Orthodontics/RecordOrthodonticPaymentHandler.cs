using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Domain.ValueObjects;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Orthodontics;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementApp.Api.Services.Implementations.Orthodontics;

/// <summary>
/// Single-responsibility handler for recording orthodontic installment payments and ledger sync.
/// Enforces Object Calisthenics: guard clauses, zero 'else' keywords, max 1 indentation level.
/// </summary>
public class RecordOrthodonticPaymentHandler(
    ApplicationDbContext context,
    ILogger<RecordOrthodonticPaymentHandler> logger)
{
    public async Task<ApiResponse<OrthodonticPaymentDto>> HandleAsync(RecordPaymentRequest request, CancellationToken ct = default)
    {
        var contract = await context.OrthodonticContracts
            .FirstOrDefaultAsync(c => c.PatientId == request.PatientId && c.IsActive, ct);

        if (contract is null)
        {
            return ApiResponse<OrthodonticPaymentDto>.FailureResult($"No active orthodontic contract found for Patient ID {request.PatientId}.");
        }

        var payment = BuildPaymentEntity(contract.Id, request);
        context.OrthodonticPayments.Add(payment);

        AddAdjustmentTreatment(payment);

        await context.SaveChangesAsync(ct);
        logger.LogInformation("Recorded orthodontic payment of ₱{Amount} for Patient #{PatientId}", payment.Amount, payment.PatientId);

        return ApiResponse<OrthodonticPaymentDto>.SuccessResult(MapToPaymentDto(payment), "Payment recorded successfully.");
    }

    private static OrthodonticPaymentEntity BuildPaymentEntity(int contractId, RecordPaymentRequest request)
    {
        var paymentAmount = new Money(request.Amount);
        var paymentDate = request.PaymentDate.HasValue
            ? DateTime.SpecifyKind(request.PaymentDate.Value, DateTimeKind.Utc)
            : DateTime.UtcNow;

        return new OrthodonticPaymentEntity
        {
            ContractId = contractId,
            PatientId = request.PatientId,
            Amount = paymentAmount.Amount,
            PaymentDate = paymentDate,
            Notes = request.Notes?.Trim() ?? string.Empty,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private void AddAdjustmentTreatment(OrthodonticPaymentEntity payment)
    {
        var treatment = new DentalTreatmentEntity
        {
            PatientId = payment.PatientId,
            TreatmentType = "Braces Adjustment",
            ConsultationFee = payment.Amount,
            DateCreated = payment.PaymentDate,
            IsInstallment = false,
            TotalContractPrice = 0,
            DownPayment = 0,
            TermsMonths = 0,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.DentalTreatments.Add(treatment);
    }

    private static OrthodonticPaymentDto MapToPaymentDto(OrthodonticPaymentEntity p) =>
        new(
            p.Id,
            p.ContractId,
            p.PatientId,
            p.Amount,
            p.PaymentDate,
            p.Notes,
            p.CreatedAtUtc
        );
}

