namespace ClinicManagementApp.Api.Models.DTOs.Reports;

public record RollingMonthlyFinancialsDto(
    string[] Labels,
    double[] IncomeValues,
    double[] ExpenseValues
);

public record DashboardFinancialSummaryDto(
    int TotalPatients,
    decimal TotalGrossIncome,
    decimal TotalExpenses,
    decimal CurrentMonthIncome,
    decimal CurrentMonthExpenses,
    int CurrentMonthPatients,
    decimal CurrentQuarterGross,
    decimal CurrentQuarterExpenses,
    RollingMonthlyFinancialsDto RollingFinancials
);

public record PeriodFinancialSummaryDto(
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetProfit,
    double IncomePercentage,
    double ExpensePercentage
);

public record TaxCalculationResultDto(
    string Regime,
    string DeductionMethod,
    int Quarter,
    int TaxYear,
    decimal GrossIncome,
    decimal AllowableDeductions,
    decimal TaxableBase,
    decimal IncomeTaxDue,
    decimal BusinessTaxDue,
    decimal TotalTaxDue,
    decimal Form2307Credits,
    decimal FinalTaxPayable,
    bool IsExempt,
    string FormName,
    string BreakdownNote
);

public record TaxDeadlineAlertDto(
    string FormCode,
    string FormTitle,
    DateTime DueDate,
    int DaysRemaining,
    decimal EstimatedAmount,
    string Severity,
    string Description
);

public record SeniorPwdDiscountDto(
    decimal ProcedureGrossFee,
    bool IsVatRegistered,
    decimal VatExemptBase,
    decimal DiscountAmount,
    decimal PatientBilledTotal,
    decimal DeductibleExpenseAmount,
    string GoverningLaw
);

