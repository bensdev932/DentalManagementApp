namespace ClinicManagementApp.Api.Models.DTOs.Users;

public record CreateStaffRequest(
    string Email,
    string Password,
    string FullName,
    string Role,
    string? Phone
);

public record UpdateProfileRequest(
    string FullName,
    string? Phone,
    string? CurrentPassword,
    string? NewPassword
);

public record AdminResetPasswordRequest(string NewPassword);

public record ToggleUserStatusRequest(bool IsActive);

