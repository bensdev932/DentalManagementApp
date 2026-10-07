using ClinicManagementApp.Api.Domain.Common;

namespace ClinicManagementApp.Api.Domain.Entities;

/// <summary>
/// Domain entity representing dental treatments, clinical procedures, and patient ledger items in PostgreSQL.
/// </summary>
public class DentalTreatmentEntity : BaseEntity
{
    public int PatientId { get; set; }
    public PatientEntity? Patient { get; set; }

    public string TreatmentType { get; set; } = string.Empty;
    public decimal ConsultationFee { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public bool IsInstallment { get; set; }
    public decimal TotalContractPrice { get; set; }
    public decimal DownPayment { get; set; }
    public int TermsMonths { get; set; }
}

