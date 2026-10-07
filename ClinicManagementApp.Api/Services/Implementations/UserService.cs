using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Auth;
using ClinicManagementApp.Api.Models.DTOs.Users;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementApp.Api.Services.Implementations;

/// <summary>
/// Implements staff management with strict Owner authorization controls.
/// </summary>
public class UserService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    ILogger<UserService> logger) : IUserService
{
    public async Task<ApiResponse<UserDto>> GetCurrentUserProfileAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ApiResponse<UserDto>.FailureResult("User not found.");
        }

        return ApiResponse<UserDto>.SuccessResult(MapToDto(user));
    }

    public async Task<ApiResponse<UserDto>> UpdateCurrentUserProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return ApiResponse<UserDto>.FailureResult("User not found.");
        }

        user.FullName = request.FullName;
        user.PhoneNumber = request.Phone;

        if (!string.IsNullOrWhiteSpace(request.CurrentPassword) && !string.IsNullOrWhiteSpace(request.NewPassword))
        {
            var changeResult = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!changeResult.Succeeded)
            {
                var errors = changeResult.Errors.Select(e => e.Description).ToList();
                return ApiResponse<UserDto>.FailureResult("Failed to change password.", errors);
            }
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = updateResult.Errors.Select(e => e.Description).ToList();
            return ApiResponse<UserDto>.FailureResult("Failed to update profile.", errors);
        }

        return ApiResponse<UserDto>.SuccessResult(MapToDto(user), "Profile updated successfully.");
    }

    public async Task<ApiResponse<IReadOnlyList<UserDto>>> GetAllStaffAsync()
    {
        var users = await userManager.Users
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var dtos = users.Select(MapToDto).ToList();
        return ApiResponse<IReadOnlyList<UserDto>>.SuccessResult(dtos);
    }

    public async Task<ApiResponse<UserDto>> CreateStaffAsync(CreateStaffRequest request)
    {
        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            return ApiResponse<UserDto>.FailureResult("An account with this email address already exists.");
        }

        var roleName = "Owner";
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        }

        var newStaff = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            PhoneNumber = request.Phone,
            Role = roleName,
            IsActive = true,
            IsApproved = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(newStaff, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            return ApiResponse<UserDto>.FailureResult("Failed to create staff account.", errors);
        }

        await userManager.AddToRoleAsync(newStaff, roleName);
        logger.LogInformation("Staff member created: {Email} with role {Role}", newStaff.Email, newStaff.Role);

        return ApiResponse<UserDto>.SuccessResult(MapToDto(newStaff), "Staff member created successfully.");
    }

    public async Task<ApiResponse<UserDto>> ToggleUserStatusAsync(Guid targetUserId, bool isActive)
    {
        var user = await userManager.FindByIdAsync(targetUserId.ToString());
        if (user == null)
        {
            return ApiResponse<UserDto>.FailureResult("Target user not found.");
        }

        var rootOwner = await userManager.Users
            .OrderBy(u => u.CreatedAtUtc)
            .ThenBy(u => u.Id)
            .FirstOrDefaultAsync();

        if (rootOwner != null && user.Id == rootOwner.Id)
        {
            return ApiResponse<UserDto>.FailureResult("Cannot disable the Owner account.");
        }

        user.IsActive = isActive;
        if (!isActive)
        {
            // Instantly invalidate active sessions by rotating security stamp
            await userManager.UpdateSecurityStampAsync(user);
            await userManager.RemoveAuthenticationTokenAsync(user, "ClinicManagementApi", "RefreshToken");
        }

        await userManager.UpdateAsync(user);
        logger.LogWarning("User status changed. Id: {UserId}, IsActive: {IsActive}", user.Id, isActive);

        return ApiResponse<UserDto>.SuccessResult(MapToDto(user), $"User account {(isActive ? "activated" : "deactivated and sessions terminated")}.");
    }

    public async Task<ApiResponse> AdminResetPasswordAsync(Guid targetUserId, string newPassword)
    {
        var user = await userManager.FindByIdAsync(targetUserId.ToString());
        if (user == null)
        {
            return ApiResponse.FailureResult("Target user not found.");
        }

        var removePassResult = await userManager.RemovePasswordAsync(user);
        if (!removePassResult.Succeeded)
        {
            var errors = removePassResult.Errors.Select(e => e.Description).ToList();
            return ApiResponse.FailureResult("Failed to clear current password.", errors);
        }

        var addPassResult = await userManager.AddPasswordAsync(user, newPassword);
        if (!addPassResult.Succeeded)
        {
            var errors = addPassResult.Errors.Select(e => e.Description).ToList();
            return ApiResponse.FailureResult("Failed to set new password.", errors);
        }

        // Kick out all active sessions on other devices
        await userManager.UpdateSecurityStampAsync(user);
        await userManager.RemoveAuthenticationTokenAsync(user, "ClinicManagementApi", "RefreshToken");

        logger.LogInformation("Password reset by Owner for user: {Email}", user.Email);
        return ApiResponse.SuccessResult("Password updated and all active sessions terminated.");
    }

    public async Task<ApiResponse> RevokeAllSessionsAsync(Guid targetUserId)
    {
        var user = await userManager.FindByIdAsync(targetUserId.ToString());
        if (user == null)
        {
            return ApiResponse.FailureResult("Target user not found.");
        }

        await userManager.UpdateSecurityStampAsync(user);
        await userManager.RemoveAuthenticationTokenAsync(user, "ClinicManagementApi", "RefreshToken");

        logger.LogInformation("All sessions revoked for user: {Email}", user.Email);
        return ApiResponse.SuccessResult("All active device sessions have been revoked.");
    }

    public async Task<ApiResponse> DeleteStaffAsync(Guid targetUserId)
    {
        var user = await userManager.FindByIdAsync(targetUserId.ToString());
        if (user == null)
        {
            return ApiResponse.FailureResult("Target user not found.");
        }

        var rootOwner = await userManager.Users
            .OrderBy(u => u.CreatedAtUtc)
            .ThenBy(u => u.Id)
            .FirstOrDefaultAsync();

        if (rootOwner != null && user.Id == rootOwner.Id)
        {
            return ApiResponse.FailureResult("Cannot delete the Owner account.");
        }

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            return ApiResponse.FailureResult("Failed to delete user account.", errors);
        }

        logger.LogInformation("User account deleted: {Email}", user.Email);
        return ApiResponse.SuccessResult("Staff account deleted permanently.");
    }

    private static UserDto MapToDto(ApplicationUser user) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.Role,
            user.PhoneNumber,
            user.IsActive,
            user.IsApproved
        );
}

