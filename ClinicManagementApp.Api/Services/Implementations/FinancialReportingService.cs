using ClinicManagementApp.Api.Common;
using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Reports;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ClinicManagementApp.Api.Services.Implementations;

/// <summary>
/// Implements dashboard analytics and financial period aggregation with memory caching.
/// </summary>
public class FinancialReportingService(ApplicationDbContext context, IMemoryCache memoryCache) : IFinancialReportingService
{
    public async Task<ApiResponse<DashboardFinancialSummaryDto>> GetDashboardSummaryAsync()
    {
        var summary = await memoryCache.GetOrCreateAsync(
            CacheKeys.DashboardSummary,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                entry.AddExpirationToken(CacheKeys.GetFinancialChangeToken());
                return await ComputeDashboardSummaryAsync();
            });

        return ApiResponse<DashboardFinancialSummaryDto>.SuccessResult(summary!);
    }

    private async Task<DashboardFinancialSummaryDto> ComputeDashboardSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var currentYear = now.Year;
        var currentMonth = now.Month;
        var currentQuarter = (currentMonth - 1) / 3 + 1;
        var quarterStartMonth = (currentQuarter - 1) * 3 + 1;
        var quarterEndMonth = quarterStartMonth + 2;

        var totalPatients = await context.Patients.CountAsync();
        var totalGrossIncome = await context.DentalTreatments.SumAsync(t => (decimal?)t.ConsultationFee) ?? 0m;
        var totalExpenses = await context.Expenses.SumAsync(e => (decimal?)e.Amount) ?? 0m;

        // Current Month
        var currentMonthIncome = await context.DentalTreatments
            .Where(t => t.DateCreated.Year == currentYear && t.DateCreated.Month == currentMonth)
            .SumAsync(t => (decimal?)t.ConsultationFee) ?? 0m;

        var currentMonthExpenses = await context.Expenses
            .Where(e => e.Date.Year == currentYear && e.Date.Month == currentMonth)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;

        var currentMonthPatients = await context.Patients
            .Where(p => p.CreatedAtUtc.Year == currentYear && p.CreatedAtUtc.Month == currentMonth)
            .CountAsync();

        // Current Quarter
        var currentQuarterGross = await context.DentalTreatments
            .Where(t => t.DateCreated.Year == currentYear && t.DateCreated.Month >= quarterStartMonth && t.DateCreated.Month <= quarterEndMonth)
            .SumAsync(t => (decimal?)t.ConsultationFee) ?? 0m;

        var currentQuarterExpenses = await context.Expenses
            .Where(e => e.Date.Year == currentYear && e.Date.Month >= quarterStartMonth && e.Date.Month <= quarterEndMonth)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;

        // 6-Month Rolling Financials
        var labels = new string[6];
        var incomeValues = new double[6];
        var expenseValues = new double[6];

        for (int i = 5; i >= 0; i--)
        {
            var targetDate = now.AddMonths(-i);
            var slotIndex = 5 - i;
            labels[slotIndex] = targetDate.ToString("MMM yyyy");

            var inc = await context.DentalTreatments
                .Where(t => t.DateCreated.Year == targetDate.Year && t.DateCreated.Month == targetDate.Month)
                .SumAsync(t => (decimal?)t.ConsultationFee) ?? 0m;

            var exp = await context.Expenses
                .Where(e => e.Date.Year == targetDate.Year && e.Date.Month == targetDate.Month)
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            incomeValues[slotIndex] = (double)inc;
            expenseValues[slotIndex] = (double)exp;
        }

        var rolling = new RollingMonthlyFinancialsDto(labels, incomeValues, expenseValues);

        return new DashboardFinancialSummaryDto(
            totalPatients,
            totalGrossIncome,
            totalExpenses,
            currentMonthIncome,
            currentMonthExpenses,
            currentMonthPatients,
            currentQuarterGross,
            currentQuarterExpenses,
            rolling
        );
    }

    public async Task<ApiResponse<PeriodFinancialSummaryDto>> GetPeriodSummaryAsync(string? period)
    {
        var cacheKey = CacheKeys.PeriodSummary(period);
        var summary = await memoryCache.GetOrCreateAsync(
            cacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                entry.AddExpirationToken(CacheKeys.GetFinancialChangeToken());
                return await ComputePeriodSummaryAsync(period);
            });

        return ApiResponse<PeriodFinancialSummaryDto>.SuccessResult(summary!);
    }

    private async Task<PeriodFinancialSummaryDto> ComputePeriodSummaryAsync(string? period)
    {
        var now = DateTime.UtcNow;
        DateTime? startDate = null;
        DateTime? endDate = null;

        switch (period?.Trim().ToLower())
        {
            case "thismonth":
                startDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                endDate = startDate.Value.AddMonths(1).AddTicks(-1);
                break;
            case "thisquarter":
                var quarter = (now.Month - 1) / 3 + 1;
                var startMonth = (quarter - 1) * 3 + 1;
                startDate = new DateTime(now.Year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
                endDate = startDate.Value.AddMonths(3).AddTicks(-1);
                break;
            case "thisyear":
                startDate = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                endDate = new DateTime(now.Year, 12, 31, 23, 59, 59, DateTimeKind.Utc);
                break;
            default: // AllTime
                break;
        }

        var treatmentQuery = context.DentalTreatments.AsNoTracking().AsQueryable();
        var expenseQuery = context.Expenses.AsNoTracking().AsQueryable();

        if (startDate.HasValue)
        {
            treatmentQuery = treatmentQuery.Where(t => t.DateCreated >= startDate.Value);
            expenseQuery = expenseQuery.Where(e => e.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            treatmentQuery = treatmentQuery.Where(t => t.DateCreated <= endDate.Value);
            expenseQuery = expenseQuery.Where(e => e.Date <= endDate.Value);
        }

        var totalIncome = await treatmentQuery.SumAsync(t => (decimal?)t.ConsultationFee) ?? 0m;
        var totalExpenses = await expenseQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m;
        var netProfit = totalIncome - totalExpenses;

        var combinedFlow = totalIncome + totalExpenses;
        var incomePct = combinedFlow > 0 ? (double)Math.Round(totalIncome / combinedFlow * 100, 1) : 0;
        var expensePct = combinedFlow > 0 ? (double)Math.Round(totalExpenses / combinedFlow * 100, 1) : 0;

        return new PeriodFinancialSummaryDto(
            totalIncome,
            totalExpenses,
            netProfit,
            incomePct,
            expensePct
        );
    }
}

