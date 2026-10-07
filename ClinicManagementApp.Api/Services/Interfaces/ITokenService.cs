using System.Security.Claims;
using ClinicManagementApp.Api.Domain.Entities;

namespace ClinicManagementApp.Api.Services.Interfaces;

/// <summary>
/// Abstraction for generating and validating JSON Web Tokens (JWT).
/// Adheres to Dependency Inversion Principle (DIP).
/// </summary>
public interface ITokenService
{
    (string Token, DateTime ExpirationUtc) GenerateAccessToken(ApplicationUser user, IList<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}

