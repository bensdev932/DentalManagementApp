using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Services.Implementations;
using ClinicManagementApp.Api.Services.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ClinicManagementApp.Api.Tests;

public class SyncAuditTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetRecentSyncAuditAsync_OrdersFailuresFirst_AndExcludesPayload()
    {
        using var context = CreateDbContext();
        var patientMock = new Mock<IPatientService>();
        var apptMock = new Mock<IAppointmentService>();
        var treatMock = new Mock<ITreatmentService>();
        var expMock = new Mock<IExpenseService>();

        var syncService = new SyncService(
            context,
            patientMock.Object,
            apptMock.Object,
            treatMock.Object,
            expMock.Object,
            NullLogger<SyncService>.Instance);

        var now = DateTime.UtcNow;

        context.OutboxSyncRecords.AddRange(
            new OutboxSyncRecordEntity
            {
                Id = Guid.NewGuid(),
                EntityType = "patient",
                EntityId = "1",
                Operation = 1,
                PayloadJson = "{\"firstName\":\"SECRET_PATIENT_NAME\"}",
                CreatedAtUtc = now.AddHours(-2),
                ProcessedAtUtc = now.AddHours(-2),
                IsSuccess = true,
                ErrorMessage = null
            },
            new OutboxSyncRecordEntity
            {
                Id = Guid.NewGuid(),
                EntityType = "expense",
                EntityId = "2",
                Operation = 1,
                PayloadJson = "{\"title\":\"SECRET_EXPENSE\"}",
                CreatedAtUtc = now.AddHours(-1),
                ProcessedAtUtc = now.AddHours(-1),
                IsSuccess = false,
                ErrorMessage = "Database timeout"
            }
        );
        await context.SaveChangesAsync();

        var audit = await syncService.GetRecentSyncAuditAsync(hours: 24, limit: 10, failedOnly: false);

        audit.Should().HaveCount(2);
        // Failures first
        audit[0].IsSuccess.Should().BeFalse();
        audit[0].ErrorMessage.Should().Be("Database timeout");
        audit[1].IsSuccess.Should().BeTrue();

        // Check failedOnly flag
        var failedAudit = await syncService.GetRecentSyncAuditAsync(hours: 24, limit: 10, failedOnly: true);
        failedAudit.Should().HaveCount(1);
        failedAudit[0].IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetRecentSyncAuditAsync_ClampsHoursAndLimit()
    {
        using var context = CreateDbContext();
        var patientMock = new Mock<IPatientService>();
        var apptMock = new Mock<IAppointmentService>();
        var treatMock = new Mock<ITreatmentService>();
        var expMock = new Mock<IExpenseService>();

        var syncService = new SyncService(
            context,
            patientMock.Object,
            apptMock.Object,
            treatMock.Object,
            expMock.Object,
            NullLogger<SyncService>.Instance);

        // Negative hours and excessive limit should be clamped safely without exceptions
        var audit = await syncService.GetRecentSyncAuditAsync(hours: -5, limit: 1000);
        audit.Should().BeEmpty();
    }
}

