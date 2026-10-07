using ClinicManagementApp.Api.Domain.Common;

namespace ClinicManagementApp.Api.Domain.Entities;

/// <summary>
/// Domain entity representing an orthodontic adjustment or monthly installment payment in PostgreSQL.
/// </summary>
public class OrthodonticPaymentEntity : BaseEntity
{
    public int ContractId { get; set; }
    public OrthodonticContractEntity? Contract { get; set; }

    public int PatientId { get; set; }
    public PatientEntity? Patient { get; set; }

    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = string.Empty;
}

