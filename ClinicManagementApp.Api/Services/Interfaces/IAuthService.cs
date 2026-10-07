using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Auth;

namespace ClinicManagementApp.Api.Services.Interfaces;

/// <summary>
/// Business logic contract for user authentication, token issuance, and owner initialization.
/// Adheres to Interface Segregation Principle (ISP).
/// </summary>
public interface IAuthService
{
    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, string expiredAccessToken);
    Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<ApiResponse<AuthResponse>> InitialOwnerSetupAsync(InitialSetupRequest request);
    Task<ApiResponse<bool>> CheckInitialSetupRequiredAsync();
}

