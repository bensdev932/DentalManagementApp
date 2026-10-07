using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Sync;

namespace ClinicManagementApp.Api.Services.Interfaces;

/// <summary>
/// Domain service contract for offline-first replication, idempotent mutations, and delta pulls.
/// </summary>
public interface ISyncService
{
    Task<ApiResponse<SyncPushResponse>> ProcessPushRecordAsync(SyncPushRequest request);
    Task<ApiResponse<IReadOnlyList<SyncBatchItemResult>>> ProcessBatchAsync(IReadOnlyList<SyncPushRequest> records);
    Task<ApiResponse<SyncPullResponse>> PullDeltaAsync(DateTime? sinceUtc);
    Task<IReadOnlyList<SyncAuditEntryDto>> GetRecentSyncAuditAsync(int hours = 72, int limit = 200, bool failedOnly = false, CancellationToken ct = default);
}

