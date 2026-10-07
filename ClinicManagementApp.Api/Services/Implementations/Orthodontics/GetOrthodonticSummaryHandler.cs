using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Domain.ValueObjects;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Orthodontics;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementApp.Api.Services.Implementations.Orthodontics;

/// <summary>
/// Single-responsibility handler for calculating orthodontic contract amortization summaries.
/// Follows Object Calisthenics (guard clauses, zero 'else' keywords, max 1 indentation level).
/// </summary>
public class GetOrthodonticSummaryHandler(ApplicationDbContext context)
{
    public async Task<ApiResponse<OrthodonticsSummaryDto>> HandleAsync(PatientId patientId, CancellationToken ct = default)
    {
        var contract = await context.OrthodonticContracts
            .AsNoTracking()
            .Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.PatientId == patientId.Value && c.IsActive, ct);

        if (contract is null)
        {
            return ApiResponse<OrthodonticsSummaryDto>.SuccessResult(CreateEmptySummary(), "No active orthodontic contract found.");
        }

        var summary = CalculateSummary(contract);
        return ApiResponse<OrthodonticsSummaryDto>.SuccessResult(summary);
    }

    private static OrthodonticsSummaryDto CalculateSummary(OrthodonticContractEntity contract)
    {
        var totalContract = new Money(contract.TotalContractPrice);
        var downPayment = new Money(contract.DownPayment);
        var installmentsSum = new Money(contract.Payments.Sum(p => p.Amount));
        var totalPaid = downPayment + installmentsSum;
        var remainingBalance = totalContract - totalPaid;

        var balanceToAmortize = totalContract - downPayment;
        var monthlyAmortization = CalculateMonthlyAmortization(balanceToAmortize, contract.TermsMonths);
        var termsRemaining = CalculateTermsRemaining(remainingBalance, monthlyAmortization);
        var progress = CalculateProgress(totalPaid, totalContract);

        return new OrthodonticsSummaryDto(
            TotalContractPrice: totalContract.Amount,
            DownPayment: downPayment.Amount,
            MonthlyAmortization: monthlyAmortization.Amount,
            TotalPaid: totalPaid.Amount,
            RemainingBalance: remainingBalance.Amount,
            TermsMonths: contract.TermsMonths,
            TermsRemaining: termsRemaining,
            PaymentProgress: progress,
            HasContract: true,
            IsActive: contract.IsActive,
            IsFullyPaid: remainingBalance.IsZero
        );
    }

    private static Money CalculateMonthlyAmortization(Money balanceToAmortize, int termsMonths)
    {
        if (termsMonths <= 0)
        {
            return Money.Zero;
        }

        return (balanceToAmortize / termsMonths).Round(2);
    }

    private static int CalculateTermsRemaining(Money remainingBalance, Money monthlyAmortization)
    {
        if (monthlyAmortization.IsZero)
        {
            return 0;
        }

        return (int)Math.Ceiling(remainingBalance.Amount / monthlyAmortization.Amount);
    }

    private static double CalculateProgress(Money totalPaid, Money totalContract)
    {
        if (totalContract.IsZero)
        {
            return 0.0;
        }

        return (double)Math.Round(totalPaid.Amount / totalContract.Amount, 4);
    }

    private static OrthodonticsSummaryDto CreateEmptySummary() =>
        new(
            TotalContractPrice: 0m,
            DownPayment: 0m,
            MonthlyAmortization: 0m,
            TotalPaid: 0m,
            RemainingBalance: 0m,
            TermsMonths: 0,
            TermsRemaining: 0,
            PaymentProgress: 0.0,
            HasContract: false,
            IsActive: false,
            IsFullyPaid: false
        );
}

