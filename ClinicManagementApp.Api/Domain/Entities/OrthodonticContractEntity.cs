using ClinicManagementApp.Api.Domain.Common;

namespace ClinicManagementApp.Api.Domain.Entities;

/// <summary>
/// Domain entity representing an orthodontic/braces contract and installment plan in PostgreSQL.
/// </summary>
public class OrthodonticContractEntity : BaseEntity
{
    public int PatientId { get; set; }
    public PatientEntity? Patient { get; set; }

    public decimal TotalContractPrice { get; set; }
    public decimal DownPayment { get; set; }
    public int TermsMonths { get; set; } = 24;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation property for installment payments
    public ICollection<OrthodonticPaymentEntity> Payments { get; set; } = [];
}

