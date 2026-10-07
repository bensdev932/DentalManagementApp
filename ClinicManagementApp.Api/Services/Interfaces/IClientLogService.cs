using ClinicManagementApp.Api.Models.DTOs.Sync;

namespace ClinicManagementApp.Api.Services.Interfaces;

public interface IClientLogService
{
    Task<ClientLogUploadResponse> IngestAsync(Guid? userId, string? userEmail, ClientLogUploadRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ClientLogViewDto>> QueryAsync(int hours = 24, string? deviceId = null, string? minLevel = null, string? correlationId = null, int limit = 500, CancellationToken ct = default);
    Task<int> PurgeAsync(DateTime olderThan, CancellationToken ct = default);
}

