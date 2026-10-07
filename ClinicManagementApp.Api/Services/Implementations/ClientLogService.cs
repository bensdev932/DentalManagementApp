using ClinicManagementApp.Api.Common;
using ClinicManagementApp.Api.Data;
using ClinicManagementApp.Api.Domain.Entities;
using ClinicManagementApp.Api.Models.DTOs.Sync;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagementApp.Api.Services.Implementations;

public class ClientLogService : IClientLogService
{
    private readonly ApplicationDbContext _dbContext;

    public ClientLogService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ClientLogUploadResponse> IngestAsync(Guid? userId, string? userEmail, ClientLogUploadRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Entries == null)
        {
            throw new ArgumentException("Entries collection cannot be null.", nameof(request));
        }

        if (request.Entries.Count > 500)
        {
            throw new ArgumentException("Exceeded maximum limit of 500 client log entries per request.", nameof(request));
        }

        if (request.Entries.Count == 0)
        {
            return new ClientLogUploadResponse(0, 0);
        }

        var incomingIds = request.Entries.Select(e => e.Id).ToList();
        var existingIds = (await _dbContext.ClientLogEntries
            .AsNoTracking()
            .Where(e => incomingIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(ct))
            .ToHashSet();

        var deviceId = Truncate(request.DeviceId, 64) ?? string.Empty;
        var deviceModel = Truncate(request.DeviceModel, 128);
        var platform = Truncate(request.Platform, 64);
        var appVersion = Truncate(request.AppVersion, 32);
        var safeEmail = Truncate(userEmail, 256);
        var now = DateTime.UtcNow;

        var entities = new List<ClientLogEntryEntity>();
        var seenIds = new HashSet<Guid>(existingIds);
        int duplicateCount = 0;

        foreach (var entry in request.Entries)
        {
            if (!seenIds.Add(entry.Id))
            {
                duplicateCount++;
                continue;
            }

            var maskedMessage = SyncLogRedactor.MaskFreeText(entry.Message);
            var maskedException = entry.Exception != null ? SyncLogRedactor.MaskFreeText(entry.Exception) : null;

            entities.Add(new ClientLogEntryEntity
            {
                Id = entry.Id,
                UserId = userId,
                UserEmail = safeEmail,
                DeviceId = deviceId,
                DeviceModel = deviceModel,
                Platform = platform,
                AppVersion = appVersion,
                TimestampUtc = entry.TimestampUtc,
                Level = Truncate(entry.Level, 20) ?? string.Empty,
                Category = Truncate(entry.Category, 200) ?? string.Empty,
                Message = Truncate(maskedMessage, 4000) ?? string.Empty,
                Exception = Truncate(maskedException, 8000),
                CorrelationId = Truncate(entry.CorrelationId, 32),
                ReceivedAtUtc = now
            });
        }

        if (entities.Count > 0)
        {
            _dbContext.ClientLogEntries.AddRange(entities);
            await _dbContext.SaveChangesAsync(ct);
        }

        return new ClientLogUploadResponse(Accepted: entities.Count, Duplicates: duplicateCount);
    }

    public async Task<IReadOnlyList<ClientLogViewDto>> QueryAsync(int hours = 24, string? deviceId = null, string? minLevel = null, string? correlationId = null, int limit = 500, CancellationToken ct = default)
    {
        if (hours < 1) hours = 1;
        if (hours > 336) hours = 336;

        if (limit < 1) limit = 1;
        if (limit > 1000) limit = 1000;

        var cutoff = DateTime.UtcNow.AddHours(-hours);
        var query = _dbContext.ClientLogEntries
            .AsNoTracking()
            .Where(e => e.TimestampUtc >= cutoff);

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            query = query.Where(e => e.DeviceId == deviceId);
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            query = query.Where(e => e.CorrelationId == correlationId);
        }

        if (!string.IsNullOrWhiteSpace(minLevel))
        {
            var allowedLevels = GetAllowedLevels(minLevel);
            if (allowedLevels.Length > 0)
            {
                query = query.Where(e => allowedLevels.Contains(e.Level.ToUpper()));
            }
        }

        return await query
            .OrderByDescending(e => e.TimestampUtc)
            .Take(limit)
            .Select(e => new ClientLogViewDto(
                e.Id,
                e.TimestampUtc,
                e.ReceivedAtUtc,
                e.UserEmail,
                e.DeviceId,
                e.DeviceModel,
                e.Platform,
                e.AppVersion,
                e.Level,
                e.Category,
                e.Message,
                e.Exception,
                e.CorrelationId
            ))
            .ToListAsync(ct);
    }

    public async Task<int> PurgeAsync(DateTime olderThan, CancellationToken ct = default)
    {
        if (_dbContext.Database.ProviderName?.Contains("InMemory") == true)
        {
            var old = await _dbContext.ClientLogEntries
                .Where(e => e.ReceivedAtUtc < olderThan)
                .ToListAsync(ct);
            _dbContext.ClientLogEntries.RemoveRange(old);
            return await _dbContext.SaveChangesAsync(ct);
        }

        return await _dbContext.ClientLogEntries
            .Where(e => e.ReceivedAtUtc < olderThan)
            .ExecuteDeleteAsync(ct);
    }

    private static string[] GetAllowedLevels(string minLevel)
    {
        var upper = minLevel.Trim().ToUpperInvariant();
        return upper switch
        {
            "TRACE" => ["TRACE", "DEBUG", "INFO", "INFORMATION", "WARN", "WARNING", "ERROR", "FATAL", "CRITICAL"],
            "DEBUG" => ["DEBUG", "INFO", "INFORMATION", "WARN", "WARNING", "ERROR", "FATAL", "CRITICAL"],
            "INFO" or "INFORMATION" => ["INFO", "INFORMATION", "WARN", "WARNING", "ERROR", "FATAL", "CRITICAL"],
            "WARN" or "WARNING" => ["WARN", "WARNING", "ERROR", "FATAL", "CRITICAL"],
            "ERROR" => ["ERROR", "FATAL", "CRITICAL"],
            "FATAL" or "CRITICAL" => ["FATAL", "CRITICAL"],
            _ => []
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value == null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

