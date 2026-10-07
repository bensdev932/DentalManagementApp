using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Domain.ValueObjects;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Orthodontics;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementApp.Api.Services.Implementations.Orthodontics;

/// <summary>
/// Single-responsibility query handler for retrieving orthodontic payment history.
/// Uses direct idiomatic LINQ expressions with AsNoTracking.
/// </summary>
public class GetOrthodonticPaymentsHandler(ApplicationDbContext context)
{
    public async Task<ApiResponse<IReadOnlyList<OrthodonticPaymentDto>>> HandleAsync(PatientId patientId, CancellationToken ct = default)
    {
        var payments = await context.OrthodonticPayments
            .AsNoTracking()
            .Where(p => p.PatientId == patientId.Value)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => MapToPaymentDto(p))
            .ToListAsync(ct);

        return ApiResponse<IReadOnlyList<OrthodonticPaymentDto>>.SuccessResult(payments);
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

