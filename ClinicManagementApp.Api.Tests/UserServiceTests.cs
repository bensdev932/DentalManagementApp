using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Models.DTOs.Users;
using ClinicManagementApp.Api.Services.Implementations;
using ClinicManagementApp.Api.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClinicManagementApp.Api.Tests;

public class UserServiceTests
{
    private static (IServiceProvider Provider, IUserService UserService, ApplicationDbContext DbContext, UserManager<ApplicationUser> UserManager) CreateTestEnvironment()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseInMemoryDatabase(dbName);
            options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        });

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 4;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.AddScoped<IUserService, UserService>();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        return (scope.ServiceProvider, userService, dbContext, userManager);
    }

    private static async Task<ApplicationUser> SeedRootOwnerAsync(UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext)
    {
        var rootOwner = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "root.owner@clinic.com",
            Email = "root.owner@clinic.com",
            FullName = "Dr. Root Owner",
            Role = "Owner",
            IsActive = true,
            IsApproved = true,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
        };

        var result = await userManager.CreateAsync(rootOwner, "Password123!");
        result.Succeeded.Should().BeTrue();
        return rootOwner;
    }

    [Fact]
    public async Task CreateStaffAsync_AssignsOwnerRole_AndSucceeds()
    {
        // Arrange
        var (_, userService, _, userManager) = CreateTestEnvironment();
        var request = new CreateStaffRequest(
            Email: "staff.test@clinic.com",
            Password: "Password123!",
            FullName: "Test Staff",
            Role: "Owner",
            Phone: "09181234567"
        );

        // Act
        var response = await userService.CreateStaffAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Role.Should().Be("Owner");

        var createdUser = await userManager.FindByEmailAsync("staff.test@clinic.com");
        createdUser.Should().NotBeNull();
        createdUser!.Role.Should().Be("Owner");
        var isInRole = await userManager.IsInRoleAsync(createdUser, "Owner");
        isInRole.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleUserStatusAsync_TargetIsRootOwner_ReturnsFailure()
    {
        // Arrange
        var (_, userService, dbContext, userManager) = CreateTestEnvironment();
        var rootOwner = await SeedRootOwnerAsync(userManager, dbContext);

        // Act - Attempt to disable the root owner
        var response = await userService.ToggleUserStatusAsync(rootOwner.Id, false);

        // Assert
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Cannot disable the Owner account.");

        var reloadedOwner = await userManager.FindByIdAsync(rootOwner.Id.ToString());
        reloadedOwner!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleUserStatusAsync_TargetIsSecondaryUser_DeactivatesAndReactivatesSuccessfully()
    {
        // Arrange
        var (_, userService, dbContext, userManager) = CreateTestEnvironment();
        var rootOwner = await SeedRootOwnerAsync(userManager, dbContext);

        var staffUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "assistant@clinic.com",
            Email = "assistant@clinic.com",
            FullName = "Clinic Assistant",
            Role = "Owner",
            IsActive = true,
            IsApproved = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await userManager.CreateAsync(staffUser, "Password123!");

        // Act 1 - Deactivate secondary account
        var deactivateResponse = await userService.ToggleUserStatusAsync(staffUser.Id, false);

        // Assert 1
        deactivateResponse.Success.Should().BeTrue();
        deactivateResponse.Data!.IsActive.Should().BeFalse();

        var reloadedUser = await userManager.FindByIdAsync(staffUser.Id.ToString());
        reloadedUser!.IsActive.Should().BeFalse();

        // Act 2 - Reactivate secondary account
        var reactivateResponse = await userService.ToggleUserStatusAsync(staffUser.Id, true);

        // Assert 2
        reactivateResponse.Success.Should().BeTrue();
        reactivateResponse.Data!.IsActive.Should().BeTrue();

        var reloadedAgain = await userManager.FindByIdAsync(staffUser.Id.ToString());
        reloadedAgain!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteStaffAsync_TargetIsRootOwner_ReturnsFailure()
    {
        // Arrange
        var (_, userService, dbContext, userManager) = CreateTestEnvironment();
        var rootOwner = await SeedRootOwnerAsync(userManager, dbContext);

        // Act
        var response = await userService.DeleteStaffAsync(rootOwner.Id);

        // Assert
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Cannot delete the Owner account.");

        var stillExists = await userManager.FindByIdAsync(rootOwner.Id.ToString());
        stillExists.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteStaffAsync_TargetIsSecondaryUser_DeletesSuccessfully()
    {
        // Arrange
        var (_, userService, dbContext, userManager) = CreateTestEnvironment();
        var rootOwner = await SeedRootOwnerAsync(userManager, dbContext);

        var staffUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "temp.staff@clinic.com",
            Email = "temp.staff@clinic.com",
            FullName = "Temporary Staff",
            Role = "Owner",
            IsActive = true,
            IsApproved = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await userManager.CreateAsync(staffUser, "Password123!");

        // Act
        var response = await userService.DeleteStaffAsync(staffUser.Id);

        // Assert
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Staff account deleted permanently.");

        var deletedUser = await userManager.FindByIdAsync(staffUser.Id.ToString());
        deletedUser.Should().BeNull();

        // Root owner should still be untouched
        var rootCheck = await userManager.FindByIdAsync(rootOwner.Id.ToString());
        rootCheck.Should().NotBeNull();
    }
}
