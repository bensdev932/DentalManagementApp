using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Reports;

namespace ClinicManagementApp.Api.Services.Interfaces;

/// <summary>
/// Domain service contract for financial aggregation, rolling revenue trends, and period reporting.
/// </summary>
public interface IFinancialReportingService
{
    Task<ApiResponse<DashboardFinancialSummaryDto>> GetDashboardSummaryAsync();
    Task<ApiResponse<PeriodFinancialSummaryDto>> GetPeriodSummaryAsync(string? period);
}

