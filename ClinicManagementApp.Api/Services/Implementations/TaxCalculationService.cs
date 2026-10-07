using ClinicManagementApp.Api.Common;
using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Reports;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ClinicManagementApp.Api.Services.Implementations;

/// <summary>
/// Implements Philippine BIR tax calculations adhering strictly to TRAIN Law (RA 10963) and BIR Form deadlines with memory caching.
/// </summary>
public class TaxCalculationService(ApplicationDbContext context, IMemoryCache memoryCache) : ITaxCalculationService
{
    public async Task<ApiResponse<TaxCalculationResultDto>> CalculateQuarterlyTaxAsync(
        int year,
        int quarter,
        string regime,
        string deductionMethod,
        decimal form2307Credits)
    {
        var cacheKey = CacheKeys.QuarterlyTax(year, quarter, regime, deductionMethod, form2307Credits);

        var result = await memoryCache.GetOrCreateAsync(
            cacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.AddExpirationToken(CacheKeys.GetFinancialChangeToken());
                return await ComputeQuarterlyTaxInternalAsync(year, quarter, regime, deductionMethod, form2307Credits);
            });

        return ApiResponse<TaxCalculationResultDto>.SuccessResult(result!);
    }

    private async Task<TaxCalculationResultDto> ComputeQuarterlyTaxInternalAsync(
        int year,
        int quarter,
        string regime,
        string deductionMethod,
        decimal form2307Credits)
    {
        if (quarter < 1 || quarter > 4) quarter = 1;
        var startMonth = (quarter - 1) * 3 + 1;
        var endMonth = startMonth + 2;

        var isEightPercent = regime.Equals("EightPercentFlat", StringComparison.OrdinalIgnoreCase);
        var isOsd = deductionMethod.Equals("OSD", StringComparison.OrdinalIgnoreCase);

        // Fetch treatments and expenses
        var treatments = await context.DentalTreatments
            .AsNoTracking()
            .Where(t => t.DateCreated.Year == year && t.DateCreated.Month <= endMonth)
            .ToListAsync();

        var expenses = await context.Expenses
            .AsNoTracking()
            .Where(e => e.Date.Year == year && e.Date.Month >= startMonth && e.Date.Month <= endMonth)
            .ToListAsync();

        // 8% flat is computed on cumulative YTD gross sales; graduated quarterly computes per quarter
        decimal grossSales = isEightPercent
            ? treatments.Sum(t => t.ConsultationFee)
            : treatments.Where(t => t.DateCreated.Month >= startMonth).Sum(t => t.ConsultationFee);

        decimal actualExpenses = expenses.Sum(e => e.Amount);

        decimal allowableDeductions;
        decimal taxableBase;
        decimal incomeTaxDue;
        decimal businessTaxDue;
        string formName;
        string note;
        bool isExempt = false;

        if (isEightPercent)
        {
            formName = "BIR Form 1701Q (8% Flat Rate)";
            businessTaxDue = 0m; // 100% exempt from 3% percentage tax

            taxableBase = Math.Max(0m, grossSales - 250000m);
            allowableDeductions = Math.Min(grossSales, 250000m);

            if (grossSales <= 250000m)
            {
                isExempt = true;
                incomeTaxDue = 0m;
                note = $"YTD Gross Sales (₱{grossSales:N2}) is within the ₱250,000 statutory exemption. ₱0.00 tax due.";
            }
            else
            {
                incomeTaxDue = Math.Round(taxableBase * 0.08m, 2);
                note = $"Taxable Base: ₱{taxableBase:N2} (YTD ₱{grossSales:N2} - ₱250,000 exemption) × 8% = ₱{incomeTaxDue:N2}.";
            }
        }
        else
        {
            formName = "BIR Form 1701Q + Form 2551Q (Graduated Rates)";
            businessTaxDue = Math.Round(grossSales * 0.03m, 2);

            allowableDeductions = isOsd
                ? Math.Round(grossSales * 0.40m, 2)
                : actualExpenses;

            taxableBase = Math.Max(0m, grossSales - allowableDeductions);

            if (grossSales <= allowableDeductions)
            {
                incomeTaxDue = 0m;
                note = $"Operating Net Loss. Income Tax: ₱0.00. 3% Percentage Tax: ₱{businessTaxDue:N2}.";
            }
            else
            {
                incomeTaxDue = Math.Round(ComputeGraduatedIncomeTax(taxableBase), 2);
                note = $"Net Taxable: ₱{taxableBase:N2}. Income Tax: ₱{incomeTaxDue:N2} + 3% Percentage Tax: ₱{businessTaxDue:N2}.";
            }
        }

        var totalTaxDue = incomeTaxDue + businessTaxDue;
        var finalTaxPayable = Math.Max(0m, totalTaxDue - form2307Credits);

        return new TaxCalculationResultDto(
            regime,
            deductionMethod,
            quarter,
            year,
            grossSales,
            allowableDeductions,
            taxableBase,
            incomeTaxDue,
            businessTaxDue,
            totalTaxDue,
            form2307Credits,
            finalTaxPayable,
            isExempt,
            formName,
            note
        );
    }

    public async Task<ApiResponse<IReadOnlyList<TaxDeadlineAlertDto>>> GetUpcomingDeadlinesAsync()
    {
        var alerts = await memoryCache.GetOrCreateAsync(
            CacheKeys.TaxDeadlines,
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                return Task.FromResult(ComputeUpcomingDeadlinesInternal());
            });

        return ApiResponse<IReadOnlyList<TaxDeadlineAlertDto>>.SuccessResult(alerts!);
    }

    private static IReadOnlyList<TaxDeadlineAlertDto> ComputeUpcomingDeadlinesInternal()
    {
        var now = DateTime.UtcNow;
        var year = now.Year;

        var deadlineSchedules = new List<(string Code, string Title, DateTime DueDate, string Desc)>
        {
            ("BIR Form 1701A", "Annual Income Tax Return (ITR)", new DateTime(year, 4, 15, 23, 59, 59, DateTimeKind.Utc), "Annual final income tax filing for self-employed dentists."),
            ("BIR Form 1701Q Q1", "Quarterly Income Tax (Q1)", new DateTime(year, 5, 15, 23, 59, 59, DateTimeKind.Utc), "1st Quarter Income Tax return."),
            ("BIR Form 1701Q Q2", "Quarterly Income Tax (Q2)", new DateTime(year, 8, 15, 23, 59, 59, DateTimeKind.Utc), "2nd Quarter Income Tax return."),
            ("BIR Form 1701Q Q3", "Quarterly Income Tax (Q3)", new DateTime(year, 11, 15, 23, 59, 59, DateTimeKind.Utc), "3rd Quarter Income Tax return."),
            ("BIR Form 2551Q Q1", "Quarterly Percentage Tax (Q1)", new DateTime(year, 4, 25, 23, 59, 59, DateTimeKind.Utc), "1st Quarter 3% Percentage Tax (Non-8% filers)."),
            ("BIR Form 2551Q Q2", "Quarterly Percentage Tax (Q2)", new DateTime(year, 7, 25, 23, 59, 59, DateTimeKind.Utc), "2nd Quarter 3% Percentage Tax (Non-8% filers)."),
            ("BIR Form 2551Q Q3", "Quarterly Percentage Tax (Q3)", new DateTime(year, 10, 25, 23, 59, 59, DateTimeKind.Utc), "3rd Quarter 3% Percentage Tax (Non-8% filers)."),
            ("BIR Form 2551Q Q4", "Quarterly Percentage Tax (Q4)", new DateTime(year + 1, 1, 25, 23, 59, 59, DateTimeKind.Utc), "4th Quarter 3% Percentage Tax (Non-8% filers).")
        };

        return deadlineSchedules
            .Select(d =>
            {
                var daysRemaining = (int)Math.Ceiling((d.DueDate - now).TotalDays);
                var severity = daysRemaining switch
                {
                    < 0 => "Critical",
                    <= 7 => "Critical",
                    <= 21 => "Warning",
                    _ => "Normal"
                };

                return new TaxDeadlineAlertDto(
                    d.Code,
                    d.Title,
                    d.DueDate,
                    daysRemaining,
                    0m, // Estimated on client or per-filing
                    severity,
                    d.Desc
                );
            })
            .OrderBy(a => a.DueDate)
            .ToList();
    }

    public ApiResponse<SeniorPwdDiscountDto> CalculateSeniorPwdDiscount(decimal grossFee, bool isVatRegistered)
    {
        if (grossFee <= 0m)
        {
            return ApiResponse<SeniorPwdDiscountDto>.FailureResult("Gross fee must be greater than zero.");
        }

        var vatExemptBase = isVatRegistered ? Math.Round(grossFee / 1.12m, 2) : grossFee;
        var discount = Math.Round(vatExemptBase * 0.20m, 2);
        var patientBilledTotal = vatExemptBase - discount;

        var result = new SeniorPwdDiscountDto(
            grossFee,
            isVatRegistered,
            vatExemptBase,
            discount,
            patientBilledTotal,
            discount,
            "RA 9994 (Senior Citizens) / RA 10754 (PWD)"
        );

        return ApiResponse<SeniorPwdDiscountDto>.SuccessResult(result);
    }

    private static decimal ComputeGraduatedIncomeTax(decimal taxableIncome) =>
        taxableIncome switch
        {
            <= 250000m => 0m,
            <= 400000m => (taxableIncome - 250000m) * 0.15m,
            <= 800000m => 22500m + ((taxableIncome - 400000m) * 0.20m),
            <= 2000000m => 102500m + ((taxableIncome - 800000m) * 0.25m),
            <= 8000000m => 402500m + ((taxableIncome - 2000000m) * 0.30m),
            _ => 2202500m + ((taxableIncome - 800000m) * 0.35m)
        };
}

