using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace ClinicManagementApp.Api.Common;

/// <summary>
/// Centralized repository of cache keys and invalidation tokens adhering to DRY and SOLID principles.
/// </summary>
public static class CacheKeys
{
    public const string DashboardSummary = "reports_dashboard_summary";
    public const string ProcedureCatalog = "catalog_procedures_all";
    public const string TaxDeadlines = "taxes_upcoming_deadlines";

    public static string PatientById(int id) => $"patient_by_id_{id}";
    public static string PatientDto(int id) => $"patient_dto_{id}";

    private static CancellationTokenSource _financialTokenSource = new();

    public static IChangeToken GetFinancialChangeToken() =>
        new CancellationChangeToken(_financialTokenSource.Token);

    public static void InvalidateFinancialData(IMemoryCache? cache = null)
    {
        var old = Interlocked.Exchange(ref _financialTokenSource, new CancellationTokenSource());
        try
        {
            old.Cancel();
            old.Dispose();
        }
        catch
        {
            // Ignore if already cancelled or disposed
        }

        if (cache != null)
        {
            cache.Remove(DashboardSummary);
            foreach (var p in StandardPeriods)
            {
                cache.Remove(PeriodSummary(p));
            }
        }
    }

    public static readonly string[] StandardPeriods = ["thismonth", "thisquarter", "thisyear", "alltime"];

    public static string NormalizePeriod(string? period)
    {
        var p = period?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(p) ? "alltime" : p;
    }

    public static string PeriodSummary(string? period) =>
        $"reports_period_{NormalizePeriod(period)}";

    public static string QuarterlyTax(int year, int quarter, string regime, string deductionMethod, decimal form2307) =>
        $"tax_quarterly_{year}_{quarter}_{regime.ToLowerInvariant()}_{deductionMethod.ToLowerInvariant()}_{form2307}";
}

