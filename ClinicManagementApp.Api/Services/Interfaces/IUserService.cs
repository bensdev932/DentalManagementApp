using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Auth;
using ClinicManagementApp.Api.Models.DTOs.Users;

namespace ClinicManagementApp.Api.Services.Interfaces;

/// <summary>
/// Service contract for user profile management and Owner-gated staff administration.
/// </summary>
public interface IUserService
{
    Task<ApiResponse<UserDto>> GetCurrentUserProfileAsync(Guid userId);
    Task<ApiResponse<UserDto>> UpdateCurrentUserProfileAsync(Guid userId, UpdateProfileRequest request);
    Task<ApiResponse<IReadOnlyList<UserDto>>> GetAllStaffAsync();
    Task<ApiResponse<UserDto>> CreateStaffAsync(CreateStaffRequest request);
    Task<ApiResponse<UserDto>> ToggleUserStatusAsync(Guid targetUserId, bool isActive);
    Task<ApiResponse> AdminResetPasswordAsync(Guid targetUserId, string newPassword);
    Task<ApiResponse> RevokeAllSessionsAsync(Guid targetUserId);
    Task<ApiResponse> DeleteStaffAsync(Guid targetUserId);
}

