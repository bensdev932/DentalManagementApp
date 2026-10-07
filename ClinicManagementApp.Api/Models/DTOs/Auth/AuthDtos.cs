namespace ClinicManagementApp.Api.Models.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record ForgotPasswordRequest(string Email);

public record InitialSetupRequest(string Email, string Password, string FullName, string? Phone);

public record UserDto(Guid Id, string Email, string FullName, string Role, string? Phone, bool IsActive, bool IsApproved);

public record AuthResponse(string Token, string RefreshToken, DateTime ExpirationUtc, UserDto User);

