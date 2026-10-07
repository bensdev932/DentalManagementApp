using ClinicManagementApp.Api.Domain.Common;

namespace ClinicManagementApp.Api.Domain.Entities;

/// <summary>
/// Domain entity representing standardized dental clinical procedures and baseline fee catalogs in PostgreSQL.
/// </summary>
public class ProcedureCatalogEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public decimal BaselinePrice { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

