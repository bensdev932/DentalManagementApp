using System.Security.Claims;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Auth;
using ClinicManagementApp.Api.Models.DTOs.Users;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagementApp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/users")]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentProfile()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(ApiResponse<UserDto>.FailureResult("Unauthorized."));

        var result = await userService.GetCurrentUserProfileAsync(userId.Value);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateCurrentProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(ApiResponse<UserDto>.FailureResult("Unauthorized."));

        var result = await userService.UpdateCurrentUserProfileAsync(userId.Value, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllStaff()
    {
        var result = await userService.GetAllStaffAsync();
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest request)
    {
        var result = await userService.CreateStaffAsync(request);
        return result.Success ? StatusCode(StatusCodes.Status201Created, result) : BadRequest(result);
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ToggleStatus(Guid id, [FromBody] ToggleUserStatusRequest request)
    {
        var result = await userService.ToggleUserStatusAsync(id, request.IsActive);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:guid}/admin-reset-password")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdminResetPassword(Guid id, [FromBody] AdminResetPasswordRequest request)
    {
        var result = await userService.AdminResetPasswordAsync(id, request.NewPassword);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/revoke-sessions")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RevokeSessions(Guid id)
    {
        var result = await userService.RevokeAllSessionsAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteStaff(Guid id)
    {
        var result = await userService.DeleteStaffAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private Guid? GetCurrentUserId()
    {
        var claimValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claimValue, out var guid) ? guid : null;
    }
}

