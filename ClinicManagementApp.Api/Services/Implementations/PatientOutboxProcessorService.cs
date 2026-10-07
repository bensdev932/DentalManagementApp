using System.Text.Json;
using ClinicManagementApp.Api.Common;
using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Models.DTOs.Patients;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ClinicManagementApp.Api.Services.Implementations;

/// <summary>
/// Background consumer processing patient creation outbox tasks using PostgreSQL FOR UPDATE SKIP LOCKED.
/// </summary>
public class PatientOutboxProcessorService(
    IServiceScopeFactory scopeFactory,
    IPatientOutboxQueueNotifier queueNotifier,
    IMemoryCache memoryCache,
    ILogger<PatientOutboxProcessorService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Patient outbox processor service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedAny = await ProcessNextBatchAsync(stoppingToken);
                if (!processedAny)
                {
                    // No pending tasks; wait for signal or fallback timeout (5 seconds)
                    await queueNotifier.WaitForNotificationAsync(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in patient outbox background processor.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        logger.LogInformation("Patient outbox processor service stopped.");
    }

    public const string PostgresLockingQuery =
        "SELECT * FROM \"PatientOutboxTasks\" WHERE \"Status\" = 'Pending' ORDER BY \"CreatedAtUtc\" ASC LIMIT {0} FOR UPDATE SKIP LOCKED";

    public async Task<bool> ProcessNextBatchAsync(CancellationToken cancellationToken = default, int batchSize = 10)
    {
        if (batchSize <= 0)
        {
            batchSize = 10;
        }

        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = context.Database.IsRelational()
                ? await context.Database.BeginTransactionAsync(cancellationToken)
                : null;

            List<PatientOutboxTaskEntity> tasks;
            if (context.Database.IsNpgsql())
            {
                tasks = await context.PatientOutboxTasks
                    .FromSqlRaw(PostgresLockingQuery, batchSize)
                    .ToListAsync(cancellationToken);
            }
            else
            {
                tasks = await context.PatientOutboxTasks
                    .Where(t => t.Status == "Pending")
                    .OrderBy(t => t.CreatedAtUtc)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);
            }

            if (tasks.Count == 0)
            {
                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
                return false;
            }

            var createdPatients = new List<PatientEntity>();

            foreach (var task in tasks)
            {
                CreatePatientRequest? request = null;
                try
                {
                    request = JsonSerializer.Deserialize<CreatePatientRequest>(task.PayloadJson, JsonOptions);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to deserialize JSON for outbox task {TaskId}", task.Id);
                }

                if (request == null || string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
                {
                    task.RetryCount++;
                    task.ErrorMessage = request == null
                        ? "Invalid or corrupted JSON payload."
                        : "First name and last name are required.";

                    if (task.RetryCount >= 3)
                    {
                        task.Status = "Failed";
                        task.ProcessedAtUtc = DateTime.UtcNow;
                        logger.LogError("Patient outbox task {TaskId} permanently failed after {RetryCount} attempts: {ErrorMessage}",
                            task.Id, task.RetryCount, task.ErrorMessage);
                    }
                    else
                    {
                        logger.LogWarning("Patient outbox task {TaskId} attempt {RetryCount} failed: {ErrorMessage}",
                            task.Id, task.RetryCount, task.ErrorMessage);
                    }
                    continue;
                }

                var patient = new PatientEntity
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Age = request.Age,
                    Gender = request.Gender?.Trim() ?? string.Empty,
                    Phone = request.Phone?.Trim() ?? string.Empty,
                    Role = string.IsNullOrWhiteSpace(request.Role) ? "Oral Prophylaxis (Cleaning)" : request.Role.Trim(),
                    ConsultationFee = request.ConsultationFee,
                    ConsultationNotes = request.ConsultationNotes?.Trim() ?? string.Empty,
                    Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim(),
                    CreatedAtUtc = DateTime.UtcNow
                };

                context.Patients.Add(patient);
                context.PatientOutboxTasks.Remove(task);
                createdPatients.Add(patient);
            }

            await context.SaveChangesAsync(cancellationToken);

            foreach (var patient in createdPatients)
            {
                var patientDto = new PatientDto(
                    patient.Id,
                    patient.FirstName,
                    patient.LastName,
                    $"{patient.FirstName} {patient.LastName}".Trim(),
                    patient.Age,
                    patient.Gender,
                    patient.Phone,
                    patient.Role,
                    patient.ConsultationFee,
                    patient.ConsultationNotes,
                    patient.Status,
                    patient.CreatedAtUtc,
                    patient.UpdatedAtUtc
                );

                var patientDetailDto = new PatientDetailDto(
                    patient.Id,
                    patient.FirstName,
                    patient.LastName,
                    $"{patient.FirstName} {patient.LastName}".Trim(),
                    patient.Age,
                    patient.Gender,
                    patient.Phone,
                    patient.Role,
                    patient.ConsultationFee,
                    patient.ConsultationNotes,
                    patient.Status,
                    patient.CreatedAtUtc,
                    patient.UpdatedAtUtc,
                    0,
                    0
                );

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                };
                memoryCache.Set(CacheKeys.PatientById(patient.Id), patientDetailDto, cacheOptions);
                memoryCache.Set(CacheKeys.PatientDto(patient.Id), patientDto, cacheOptions);
            }

            if (createdPatients.Count > 0)
            {
                CacheKeys.InvalidateFinancialData(memoryCache);
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            logger.LogInformation("Processed batch of {TaskCount} outbox tasks ({CreatedCount} patients created).",
                tasks.Count, createdPatients.Count);

            return true;
        });
    }
}
