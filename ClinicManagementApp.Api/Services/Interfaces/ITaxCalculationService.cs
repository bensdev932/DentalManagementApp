using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Reports;

namespace ClinicManagementApp.Api.Services.Interfaces;

/// <summary>
/// Domain service contract for Philippine BIR Tax computations under TRAIN Law (RA 10963) and EOPT Law.
/// </summary>
public interface ITaxCalculationService
{
    Task<ApiResponse<TaxCalculationResultDto>> CalculateQuarterlyTaxAsync(
        int year,
        int quarter,
        string regime,
        string deductionMethod,
        decimal form2307Credits);

    Task<ApiResponse<IReadOnlyList<TaxDeadlineAlertDto>>> GetUpcomingDeadlinesAsync();

    ApiResponse<SeniorPwdDiscountDto> CalculateSeniorPwdDiscount(decimal grossFee, bool isVatRegistered);
}

