using ClinicManagementApp.Api.Domain.Common;

namespace ClinicManagementApp.Api.Domain.Entities;

/// <summary>
/// Domain entity representing a clinic patient in PostgreSQL.
/// </summary>
public class PatientEntity : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = "Oral Prophylaxis (Cleaning)";
    public decimal ConsultationFee { get; set; }
    public string ConsultationNotes { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";

    // Navigation properties
    public ICollection<AppointmentEntity> Appointments { get; set; } = [];
    public ICollection<DentalTreatmentEntity> Treatments { get; set; } = [];
    public ICollection<OrthodonticContractEntity> OrthodonticContracts { get; set; } = [];
}

