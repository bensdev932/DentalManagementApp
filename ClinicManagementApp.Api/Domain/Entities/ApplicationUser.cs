using Microsoft.AspNetCore.Identity;

namespace ClinicManagementApp.Api.Domain.Entities;

/// <summary>
/// Domain entity representing a clinic staff member or owner with full administrative gating flags.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Functional role within the clinic: Owner.
    /// </summary>
    public string Role { get; set; } = "Owner";

    /// <summary>
    /// Master kill-switch. When toggled to false by the Owner, all tokens and login attempts are immediately rejected.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Explicit approval flag governed solely by the Owner.
    /// </summary>
    public bool IsApproved { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAtUtc { get; set; }
}

