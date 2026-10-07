using System.Text.Json;
using ClinicManagementApp.Api.Common;
using ClinicManagementApp.Api.Controllers.V1;
using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Patients;
using ClinicManagementApp.Api.Services.Implementations;
using ClinicManagementApp.Api.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ClinicManagementApp.Api.Tests;

[Collection("FinancialCacheTests")]
public class PatientOutboxTests
{
    private static (IServiceProvider Provider, IServiceScopeFactory ScopeFactory, ApplicationDbContext Context, IMemoryCache Cache) CreateTestEnvironment(string? dbName = null)
    {
        var services = new ServiceCollection();
        var databaseName = dbName ?? Guid.NewGuid().ToString();

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseInMemoryDatabase(databaseName: databaseName);
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
        });
        services.AddMemoryCache();

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var context = provider.GetRequiredService<ApplicationDbContext>();
        var cache = provider.GetRequiredService<IMemoryCache>();

        return (provider, scopeFactory, context, cache);
    }

    [Fact]
    public async Task CreatePatient_Returns202Accepted_WithPendingStatus()
    {
        // Arrange
        var patientServiceMock = new Mock<IPatientService>();
        var request = new CreatePatientRequest(
            "Maria",
            "Clara",
            24,
            "Female",
            "09171234567",
            "Oral Prophylaxis (Cleaning)",
            1500m,
            "First visit",
            "Active"
        );

        var pendingDto = new PatientDto(
            0,
            "Maria",
            "Clara",
            "Maria Clara",
            24,
            "Female",
            "09171234567",
            "Oral Prophylaxis (Cleaning)",
            1500m,
            "First visit",
            "Pending",
            DateTime.UtcNow,
            null
        );

        patientServiceMock
            .Setup(s => s.EnqueuePatientCreationAsync(request))
            .ReturnsAsync(ApiResponse<PatientDto>.SuccessResult(pendingDto, "Patient creation task enqueued successfully."));

        var controller = new PatientsController(patientServiceMock.Object);

        // Act
        var result = await controller.CreatePatient(request);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
        var acceptedResult = (AcceptedResult)result;
        acceptedResult.StatusCode.Should().Be(202);

        var apiResponse = acceptedResult.Value.Should().BeOfType<ApiResponse<PatientDto>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Status.Should().Be("Pending");
        apiResponse.Data.FirstName.Should().Be("Maria");
        apiResponse.Data.LastName.Should().Be("Clara");
    }

    [Fact]
    public async Task CreatePatient_Returns400BadRequest_WhenValidationFails()
    {
        // Arrange
        var patientServiceMock = new Mock<IPatientService>();
        var request = new CreatePatientRequest("", "", 0, "", "", null, 0m, null, null);

        patientServiceMock
            .Setup(s => s.EnqueuePatientCreationAsync(request))
            .ReturnsAsync(ApiResponse<PatientDto>.FailureResult("First name and last name are required."));

        var controller = new PatientsController(patientServiceMock.Object);

        // Act
        var result = await controller.CreatePatient(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.StatusCode.Should().Be(400);

        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse<PatientDto>>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("required");
    }

    [Fact]
    public async Task EnqueuePatientCreationAsync_WritesOutboxRecord_AndNotifiesQueue()
    {
        // Arrange
        var (_, _, context, cache) = CreateTestEnvironment();
        var notifierMock = new Mock<IPatientOutboxQueueNotifier>();
        var logger = NullLogger<PatientService>.Instance;

        var patientService = new PatientService(context, cache, notifierMock.Object, logger);
        var request = new CreatePatientRequest(
            "Juan",
            "Dela Cruz",
            35,
            "Male",
            "09181112233",
            "Restoration",
            2000m,
            "Allergic to penicillin",
            "Active"
        );

        // Act
        var result = await patientService.EnqueuePatientCreationAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be("Pending");
        result.Data.FullName.Should().Be("Juan Dela Cruz");

        // Verify record in Outbox table
        var tasks = await context.PatientOutboxTasks.ToListAsync();
        tasks.Should().HaveCount(1);
        tasks[0].Status.Should().Be("Pending");
        tasks[0].RetryCount.Should().Be(0);
        tasks[0].PayloadJson.Should().Contain("Juan");

        // Verify sub-millisecond signal emitted
        notifierMock.Verify(n => n.Notify(), Times.Once);
    }

    [Fact]
    public async Task Worker_ProcessNextBatchAsync_SavesPatient_AndClearsOutboxQueue()
    {
        // Arrange
        var (_, scopeFactory, context, cache) = CreateTestEnvironment();
        var notifierMock = new Mock<IPatientOutboxQueueNotifier>();
        var logger = NullLogger<PatientOutboxProcessorService>.Instance;

        var request = new CreatePatientRequest(
            "Jose",
            "Rizal",
            35,
            "Male",
            "09192223344",
            "Oral Prophylaxis (Cleaning)",
            1200m,
            "Calamba clinic",
            "Active"
        );

        var task = new PatientOutboxTaskEntity
        {
            Id = Guid.NewGuid(),
            PayloadJson = JsonSerializer.Serialize(request),
            Status = "Pending",
            RetryCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.PatientOutboxTasks.Add(task);
        await context.SaveChangesAsync();

        var worker = new PatientOutboxProcessorService(scopeFactory, notifierMock.Object, cache, logger);

        // Act
        var processed = await worker.ProcessNextBatchAsync();

        // Assert
        processed.Should().BeTrue();

        // 1. Final patient record saved in Patients table via EF Core
        var savedPatient = await context.Patients.FirstOrDefaultAsync(p => p.LastName == "Rizal");
        savedPatient.Should().NotBeNull();
        savedPatient!.FirstName.Should().Be("Jose");
        savedPatient.Phone.Should().Be("09192223344");
        savedPatient.ConsultationFee.Should().Be(1200m);

        // 2. Queue is cleared (task removed from Outbox)
        var remainingTasks = await context.PatientOutboxTasks.ToListAsync();
        remainingTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task Worker_WarmsMemoryCache_AndSubsequentReadsAreImmediate()
    {
        // Arrange
        var (_, scopeFactory, context, cache) = CreateTestEnvironment();
        var notifierMock = new Mock<IPatientOutboxQueueNotifier>();
        var workerLogger = NullLogger<PatientOutboxProcessorService>.Instance;
        var serviceLogger = NullLogger<PatientService>.Instance;

        var request = new CreatePatientRequest(
            "Andres",
            "Bonifacio",
            29,
            "Male",
            "09203334455",
            "Tooth Extraction",
            800m,
            "Tondo clinic",
            "Active"
        );

        var task = new PatientOutboxTaskEntity
        {
            Id = Guid.NewGuid(),
            PayloadJson = JsonSerializer.Serialize(request),
            Status = "Pending",
            RetryCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.PatientOutboxTasks.Add(task);
        await context.SaveChangesAsync();

        var worker = new PatientOutboxProcessorService(scopeFactory, notifierMock.Object, cache, workerLogger);
        var patientService = new PatientService(context, cache, notifierMock.Object, serviceLogger);

        // Act - Background worker processes the queue
        await worker.ProcessNextBatchAsync();

        var createdPatient = await context.Patients.FirstAsync(p => p.LastName == "Bonifacio");

        // Assert - Memory cache is immediately warmed
        var cacheKeyDetail = CacheKeys.PatientById(createdPatient.Id);
        var cacheKeyDto = CacheKeys.PatientDto(createdPatient.Id);

        cache.TryGetValue(cacheKeyDetail, out PatientDetailDto? cachedDetail).Should().BeTrue();
        cachedDetail.Should().NotBeNull();
        cachedDetail!.FullName.Should().Be("Andres Bonifacio");

        cache.TryGetValue(cacheKeyDto, out PatientDto? cachedDto).Should().BeTrue();
        cachedDto.Should().NotBeNull();
        cachedDto!.FullName.Should().Be("Andres Bonifacio");

        // Read through PatientService hits cache directly
        var serviceRead = await patientService.GetPatientByIdAsync(createdPatient.Id);
        serviceRead.Success.Should().BeTrue();
        serviceRead.Data!.FullName.Should().Be("Andres Bonifacio");
    }

    [Fact]
    public async Task PatientService_UpdateAndDelete_EvictMemoryCache()
    {
        // Arrange
        var (_, _, context, cache) = CreateTestEnvironment();
        var notifierMock = new Mock<IPatientOutboxQueueNotifier>();
        var serviceLogger = NullLogger<PatientService>.Instance;

        var patient = new PatientEntity
        {
            FirstName = "Melchora",
            LastName = "Aquino",
            Age = 84,
            Gender = "Female",
            Phone = "09214445566",
            Role = "Oral Prophylaxis (Cleaning)",
            ConsultationFee = 500m,
            Status = "Active",
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var patientService = new PatientService(context, cache, notifierMock.Object, serviceLogger);

        // Populate cache
        await patientService.GetPatientByIdAsync(patient.Id);
        cache.TryGetValue(CacheKeys.PatientById(patient.Id), out _).Should().BeTrue();

        // Act 1: Update patient
        var updateResult = await patientService.UpdatePatientAsync(patient.Id, new UpdatePatientRequest(
            "Melchora",
            "Aquino Updated",
            84,
            "Female",
            "09214445566",
            "Oral Prophylaxis (Cleaning)",
            600m,
            "Updated notes",
            "Active"
        ));
        updateResult.Success.Should().BeTrue();

        // Assert: Cache was evicted
        cache.TryGetValue(CacheKeys.PatientById(patient.Id), out _).Should().BeFalse();

        // Re-populate cache
        await patientService.GetPatientByIdAsync(patient.Id);
        cache.TryGetValue(CacheKeys.PatientById(patient.Id), out _).Should().BeTrue();

        // Act 2: Delete patient
        var deleteResult = await patientService.DeletePatientAsync(patient.Id);
        deleteResult.Success.Should().BeTrue();

        // Assert: Cache was evicted
        cache.TryGetValue(CacheKeys.PatientById(patient.Id), out _).Should().BeFalse();
    }

    [Fact]
    public async Task Worker_DeadLetters_CorruptedPayloads_After3Retries()
    {
        // Arrange
        var (_, scopeFactory, context, cache) = CreateTestEnvironment();
        var notifierMock = new Mock<IPatientOutboxQueueNotifier>();
        var logger = NullLogger<PatientOutboxProcessorService>.Instance;

        var corruptedTask = new PatientOutboxTaskEntity
        {
            Id = Guid.NewGuid(),
            PayloadJson = "{ corrupt_json: true, missing_braces ",
            Status = "Pending",
            RetryCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.PatientOutboxTasks.Add(corruptedTask);
        await context.SaveChangesAsync();

        var worker = new PatientOutboxProcessorService(scopeFactory, notifierMock.Object, cache, logger);

        // Attempt 1
        var result1 = await worker.ProcessNextBatchAsync();
        result1.Should().BeTrue();
        var t1 = await context.PatientOutboxTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == corruptedTask.Id);
        t1!.RetryCount.Should().Be(1);
        t1.Status.Should().Be("Pending");
        t1.ErrorMessage.Should().NotBeNullOrEmpty();

        // Attempt 2
        var result2 = await worker.ProcessNextBatchAsync();
        result2.Should().BeTrue();
        var t2 = await context.PatientOutboxTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == corruptedTask.Id);
        t2!.RetryCount.Should().Be(2);
        t2.Status.Should().Be("Pending");

        // Attempt 3: Isolation / Dead-lettering
        var result3 = await worker.ProcessNextBatchAsync();
        result3.Should().BeTrue();
        var t3 = await context.PatientOutboxTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == corruptedTask.Id);
        t3!.RetryCount.Should().Be(3);
        t3.Status.Should().Be("Failed");
        t3.ProcessedAtUtc.Should().NotBeNull();

        // Attempt 4: Worker ignores Failed task; returns false as no pending tasks remain
        var result4 = await worker.ProcessNextBatchAsync();
        result4.Should().BeFalse();

        // Ensure no phantom patient was created
        var patientCount = await context.Patients.CountAsync();
        patientCount.Should().Be(0);
    }

    [Fact]
    public async Task QueueNotifier_SignalingAndTimeout_BehavesAsExpected()
    {
        var notifier = new PatientOutboxQueueNotifier();

        // Initial wait with short timeout returns false
        var signaledBefore = await notifier.WaitForNotificationAsync(TimeSpan.FromMilliseconds(50));
        signaledBefore.Should().BeFalse();

        // Emit signal
        notifier.Notify();

        // Subsequent wait immediately returns true
        var signaledAfter = await notifier.WaitForNotificationAsync(TimeSpan.FromSeconds(1));
        signaledAfter.Should().BeTrue();
    }

    [Fact]
    public void PostgresLockingQuery_MatchesContract_WithSkipLocked()
    {
        // Assert the exact Postgres FOR UPDATE SKIP LOCKED query contract
        PatientOutboxProcessorService.PostgresLockingQuery.Should().Be(
            "SELECT * FROM \"PatientOutboxTasks\" WHERE \"Status\" = 'Pending' ORDER BY \"CreatedAtUtc\" ASC LIMIT {0} FOR UPDATE SKIP LOCKED"
        );
    }

    [Fact]
    public async Task Worker_FallbackExecutionPath_WhenNonNpgsqlProvider_ProcessesSuccessfully()
    {
        // Arrange - InMemory database provider exercises context.Database.IsNpgsql() == false fallback
        var (_, scopeFactory, context, cache) = CreateTestEnvironment();
        var notifierMock = new Mock<IPatientOutboxQueueNotifier>();
        var logger = NullLogger<PatientOutboxProcessorService>.Instance;

        context.Database.IsNpgsql().Should().BeFalse("in-memory database is a non-Npgsql provider");

        var request = new CreatePatientRequest(
            "Emilio",
            "Aguinaldo",
            28,
            "Male",
            "09170001122",
            "Consultation",
            1000m,
            "Kawit clinic",
            "Active"
        );

        var task = new PatientOutboxTaskEntity
        {
            Id = Guid.NewGuid(),
            PayloadJson = JsonSerializer.Serialize(request),
            Status = "Pending",
            RetryCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.PatientOutboxTasks.Add(task);
        await context.SaveChangesAsync();

        var worker = new PatientOutboxProcessorService(scopeFactory, notifierMock.Object, cache, logger);

        // Act - Executes the fallback LINQ path without crashing on non-relational transactions
        var processed = await worker.ProcessNextBatchAsync();

        // Assert
        processed.Should().BeTrue();
        var patient = await context.Patients.FirstOrDefaultAsync(p => p.LastName == "Aguinaldo");
        patient.Should().NotBeNull();
        patient!.FirstName.Should().Be("Emilio");

        var remainingTasks = await context.PatientOutboxTasks.ToListAsync();
        remainingTasks.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "Dela Cruz")]
    [InlineData("   ", "Dela Cruz")]
    [InlineData("Juan", "")]
    [InlineData("Juan", "   ")]
    public async Task EnqueuePatientCreationAsync_RejectsEmptyFirstOrLastName(string firstName, string lastName)
    {
        // Arrange
        var (_, _, context, cache) = CreateTestEnvironment();
        var notifierMock = new Mock<IPatientOutboxQueueNotifier>();
        var logger = NullLogger<PatientService>.Instance;

        var patientService = new PatientService(context, cache, notifierMock.Object, logger);
        var request = new CreatePatientRequest(firstName, lastName, 30, "Male", "09171112233", null, 0m, null, null);

        // Act
        var result = await patientService.EnqueuePatientCreationAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("required");
        (await context.PatientOutboxTasks.CountAsync()).Should().Be(0);
        notifierMock.Verify(n => n.Notify(), Times.Never);
    }

    [Fact]
    public async Task Worker_CustomBatchSize_ProcessesUpToRequestedBatchSize()
    {
        // Arrange
        var (_, scopeFactory, context, cache) = CreateTestEnvironment();
        var notifierMock = new Mock<IPatientOutboxQueueNotifier>();
        var logger = NullLogger<PatientOutboxProcessorService>.Instance;

        for (int i = 1; i <= 5; i++)
        {
            var req = new CreatePatientRequest($"Patient{i}", "Test", 20 + i, "Other", $"0918000000{i}", null, 500m, null, "Active");
            context.PatientOutboxTasks.Add(new PatientOutboxTaskEntity
            {
                Id = Guid.NewGuid(),
                PayloadJson = JsonSerializer.Serialize(req),
                Status = "Pending",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(i)
            });
        }
        await context.SaveChangesAsync();

        var worker = new PatientOutboxProcessorService(scopeFactory, notifierMock.Object, cache, logger);

        // Act - Request batchSize = 2
        var processed = await worker.ProcessNextBatchAsync(CancellationToken.None, batchSize: 2);

        // Assert
        processed.Should().BeTrue();
        var createdCount = await context.Patients.CountAsync();
        createdCount.Should().Be(2);

        var remainingCount = await context.PatientOutboxTasks.CountAsync(t => t.Status == "Pending");
        remainingCount.Should().Be(3);
    }
}
