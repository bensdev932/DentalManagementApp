using System.Security.Claims;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Auth;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementApp.Api.Services.Implementations;

/// <summary>
/// Implements business logic for authentication, owner verification, and token issuance.
/// </summary>
public class AuthService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    ITokenService tokenService,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return ApiResponse<AuthResponse>.FailureResult("Invalid email or password.");
        }

        // 1. Owner Kill Switch Check
        if (!user.IsActive)
        {
            logger.LogWarning("Login attempt for deactivated user: {Email}", request.Email);
            return ApiResponse<AuthResponse>.FailureResult("Your account has been suspended by the clinic owner.");
        }

        // 2. Owner Approval Check
        if (!user.IsApproved)
        {
            logger.LogWarning("Login attempt for unapproved user: {Email}", request.Email);
            return ApiResponse<AuthResponse>.FailureResult("Your account is pending owner authorization.");
        }

        // 3. Password Verification
        var isPasswordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            return ApiResponse<AuthResponse>.FailureResult("Invalid email or password.");
        }

        // Update login audit timestamp
        user.LastLoginAtUtc = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var (token, expiration) = tokenService.GenerateAccessToken(user, roles);
        var refreshToken = tokenService.GenerateRefreshToken();

        // Store refresh token in user tokens table
        await userManager.SetAuthenticationTokenAsync(user, "ClinicManagementApi", "RefreshToken", refreshToken);

        var userDto = new UserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.Role,
            user.PhoneNumber,
            user.IsActive,
            user.IsApproved
        );

        return ApiResponse<AuthResponse>.SuccessResult(
            new AuthResponse(token, refreshToken, expiration, userDto),
            "Authentication successful.");
    }

    public async Task<ApiResponse<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, string expiredAccessToken)
    {
        var principal = tokenService.GetPrincipalFromExpiredToken(expiredAccessToken);
        if (principal == null)
        {
            return ApiResponse<AuthResponse>.FailureResult("Invalid access token.");
        }

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return ApiResponse<AuthResponse>.FailureResult("Invalid token subject.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null || !user.IsActive || !user.IsApproved)
        {
            return ApiResponse<AuthResponse>.FailureResult("Account is invalid, inactive, or unapproved.");
        }

        var savedRefreshToken = await userManager.GetAuthenticationTokenAsync(user, "ClinicManagementApi", "RefreshToken");
        if (savedRefreshToken != request.RefreshToken)
        {
            return ApiResponse<AuthResponse>.FailureResult("Invalid or expired refresh token.");
        }

        var roles = await userManager.GetRolesAsync(user);
        var (newToken, expiration) = tokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = tokenService.GenerateRefreshToken();

        await userManager.SetAuthenticationTokenAsync(user, "ClinicManagementApi", "RefreshToken", newRefreshToken);

        var userDto = new UserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.Role,
            user.PhoneNumber,
            user.IsActive,
            user.IsApproved
        );

        return ApiResponse<AuthResponse>.SuccessResult(
            new AuthResponse(newToken, newRefreshToken, expiration, userDto),
            "Token refreshed successfully.");
    }

    public async Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive)
        {
            // Return success without revealing user existence (security best practice)
            return ApiResponse.SuccessResult("If the account exists, a password reset procedure has been initiated.");
        }

        // In production, dispatch email/SMS with reset token.
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        logger.LogInformation("Password reset token generated for user: {Email}, Token: {Token}", user.Email, resetToken);

        return ApiResponse.SuccessResult("Password reset instructions dispatched.");
    }

    public async Task<ApiResponse<AuthResponse>> InitialOwnerSetupAsync(InitialSetupRequest request)
    {
        // One-Time Setup Guard: Only allowed if zero users exist in the database!
        var existingUserCount = await userManager.Users.CountAsync();
        if (existingUserCount > 0)
        {
            return ApiResponse<AuthResponse>.FailureResult("Initial owner setup has already been completed. Contact the clinic owner.");
        }

        // Ensure roles exist
        string[] standardRoles = ["Owner"];
        foreach (var role in standardRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var owner = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            PhoneNumber = request.Phone,
            Role = "Owner",
            IsActive = true,
            IsApproved = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(owner, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            return ApiResponse<AuthResponse>.FailureResult("Failed to create owner account.", errors);
        }

        await userManager.AddToRoleAsync(owner, "Owner");

        var roles = await userManager.GetRolesAsync(owner);
        var (token, expiration) = tokenService.GenerateAccessToken(owner, roles);
        var refreshToken = tokenService.GenerateRefreshToken();

        await userManager.SetAuthenticationTokenAsync(owner, "ClinicManagementApi", "RefreshToken", refreshToken);

        var userDto = new UserDto(
            owner.Id,
            owner.Email,
            owner.FullName,
            owner.Role,
            owner.PhoneNumber,
            owner.IsActive,
            owner.IsApproved
        );

        return ApiResponse<AuthResponse>.SuccessResult(
            new AuthResponse(token, refreshToken, expiration, userDto),
            "Owner account initialized successfully.");
    }

    public async Task<ApiResponse<bool>> CheckInitialSetupRequiredAsync()
    {
        var hasUsers = await userManager.Users.AnyAsync();
        return ApiResponse<bool>.SuccessResult(!hasUsers, hasUsers ? "System initialized." : "Initial owner setup required.");
    }
}

