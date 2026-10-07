using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Models.DTOs.Sync;
using ClinicManagementApp.Api.Services.Implementations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementApp.Api.Tests;

public class ClientLogServiceTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task IngestAsync_InsertsNewEntries_AndSkipsDuplicates()
    {
        using var context = CreateDbContext();
        var service = new ClientLogService(context);

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var request1 = new ClientLogUploadRequest(
            "dev-1", "Pixel 8", "Android 14", "1.0.0",
            [
                new ClientLogEntryDto(id1, DateTime.UtcNow, "Information", "Sync", "Test message 1", null, "cid-1")
            ]);

        var result1 = await service.IngestAsync(null, "user@clinic.com", request1);
        result1.Accepted.Should().Be(1);
        result1.Duplicates.Should().Be(0);

        // Upload again with id1 and new id2
        var request2 = new ClientLogUploadRequest(
            "dev-1", "Pixel 8", "Android 14", "1.0.0",
            [
                new ClientLogEntryDto(id1, DateTime.UtcNow, "Information", "Sync", "Duplicate message", null, "cid-1"),
                new ClientLogEntryDto(id2, DateTime.UtcNow, "Warning", "Sync", "New message 2", null, "cid-2")
            ]);

        var result2 = await service.IngestAsync(null, "user@clinic.com", request2);
        result2.Accepted.Should().Be(1);
        result2.Duplicates.Should().Be(1);

        var count = await context.ClientLogEntries.CountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task IngestAsync_RejectsMoreThan500Entries()
    {
        using var context = CreateDbContext();
        var service = new ClientLogService(context);

        var entries = Enumerable.Range(1, 501)
            .Select(i => new ClientLogEntryDto(Guid.NewGuid(), DateTime.UtcNow, "Info", "Category", $"Msg {i}", null, null))
            .ToList();

        var request = new ClientLogUploadRequest("dev-1", null, null, null, entries);

        var act = () => service.IngestAsync(null, null, request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*500*");
    }

    [Fact]
    public async Task IngestAsync_TruncatesOverlengthFields()
    {
        using var context = CreateDbContext();
        var service = new ClientLogService(context);

        var longMsg = new string('A', 5000);
        var longCat = new string('C', 300);
        var longDeviceId = new string('D', 100);
        var id = Guid.NewGuid();

        var request = new ClientLogUploadRequest(
            longDeviceId, null, null, null,
            [
                new ClientLogEntryDto(id, DateTime.UtcNow, "Information", longCat, longMsg, null, "cid")
            ]);

        await service.IngestAsync(null, null, request);

        var saved = await context.ClientLogEntries.FindAsync(id);
        saved.Should().NotBeNull();
        saved!.Message.Length.Should().Be(4000);
        saved.Category.Length.Should().Be(200);
        saved.DeviceId.Length.Should().Be(64);
    }

    [Fact]
    public async Task QueryAsync_AppliesFiltersAndOrdering()
    {
        using var context = CreateDbContext();
        var service = new ClientLogService(context);

        var baseTime = DateTime.UtcNow;

        context.ClientLogEntries.AddRange(
            new ClientLogEntryEntity
            {
                Id = Guid.NewGuid(),
                DeviceId = "dev-1",
                CorrelationId = "cid-abc",
                Level = "INFO",
                Category = "Sync",
                Message = "Old entry",
                TimestampUtc = baseTime.AddHours(-10),
                ReceivedAtUtc = baseTime.AddHours(-10)
            },
            new ClientLogEntryEntity
            {
                Id = Guid.NewGuid(),
                DeviceId = "dev-1",
                CorrelationId = "cid-abc",
                Level = "WARNING",
                Category = "Sync",
                Message = "Recent warn entry",
                TimestampUtc = baseTime.AddHours(-1),
                ReceivedAtUtc = baseTime.AddHours(-1)
            },
            new ClientLogEntryEntity
            {
                Id = Guid.NewGuid(),
                DeviceId = "dev-2",
                CorrelationId = "cid-xyz",
                Level = "ERROR",
                Category = "Sync",
                Message = "Dev-2 error entry",
                TimestampUtc = baseTime.AddMinutes(-10),
                ReceivedAtUtc = baseTime.AddMinutes(-10)
            }
        );
        await context.SaveChangesAsync();

        // Filter by deviceId
        var dev1Logs = await service.QueryAsync(hours: 24, deviceId: "dev-1");
        dev1Logs.Should().HaveCount(2);

        // Filter by minLevel = WARN
        var warnLogs = await service.QueryAsync(hours: 24, minLevel: "WARN");
        warnLogs.Should().HaveCount(2);
        warnLogs.Select(x => x.Level).Should().Contain(["WARNING", "ERROR"]);

        // Filter by correlationId
        var cidLogs = await service.QueryAsync(hours: 24, correlationId: "cid-abc");
        cidLogs.Should().HaveCount(2);

        // Filter by cutoff hours = 2 (should exclude -10h entry)
        var recentLogs = await service.QueryAsync(hours: 2);
        recentLogs.Should().HaveCount(2);
    }

    [Fact]
    public async Task PurgeAsync_RemovesOnlyOlderEntries()
    {
        using var context = CreateDbContext();
        var service = new ClientLogService(context);

        var now = DateTime.UtcNow;

        var idOld = Guid.NewGuid();
        var idNew = Guid.NewGuid();

        context.ClientLogEntries.AddRange(
            new ClientLogEntryEntity
            {
                Id = idOld,
                DeviceId = "dev",
                Level = "INFO",
                Category = "Cat",
                Message = "Old",
                TimestampUtc = now.AddDays(-20),
                ReceivedAtUtc = now.AddDays(-20)
            },
            new ClientLogEntryEntity
            {
                Id = idNew,
                DeviceId = "dev",
                Level = "INFO",
                Category = "Cat",
                Message = "New",
                TimestampUtc = now.AddDays(-2),
                ReceivedAtUtc = now.AddDays(-2)
            }
        );
        await context.SaveChangesAsync();

        var cutoff = now.AddDays(-14);
        var purged = await service.PurgeAsync(cutoff);
        purged.Should().Be(1);

        var remaining = await context.ClientLogEntries.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().Be(idNew);
    }

    [Fact]
    public async Task IngestAsync_IntraBatchDuplicateIds_AcceptsFirstAndCountsDuplicate()
    {
        using var context = CreateDbContext();
        var service = new ClientLogService(context);

        var duplicateId = Guid.NewGuid();
        var uniqueId = Guid.NewGuid();

        var request = new ClientLogUploadRequest(
            "dev-1", "Pixel 8", "Android 14", "1.0.0",
            [
                new ClientLogEntryDto(duplicateId, DateTime.UtcNow, "Information", "Sync", "First instance", null, "cid-1"),
                new ClientLogEntryDto(duplicateId, DateTime.UtcNow, "Information", "Sync", "Second duplicate instance", null, "cid-2"),
                new ClientLogEntryDto(uniqueId, DateTime.UtcNow, "Warning", "Sync", "Unique entry", null, "cid-3")
            ]);

        var result = await service.IngestAsync(null, "user@clinic.com", request);

        result.Accepted.Should().Be(2);
        result.Duplicates.Should().Be(1);

        var count = await context.ClientLogEntries.CountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task IngestAsync_NullEntries_ThrowsArgumentException()
    {
        using var context = CreateDbContext();
        var service = new ClientLogService(context);

        var request = new ClientLogUploadRequest("dev-1", null, null, null, null!);

        var act = () => service.IngestAsync(null, null, request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Entries*");
    }
}

