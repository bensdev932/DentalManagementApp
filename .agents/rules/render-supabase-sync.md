---
trigger: model_decision
description: Architectural rules for .NET MAUI, Render hosting, and Supabase database synchronization.
---

# Render & Supabase Synchronization Architecture Rules

1. **Keep-Alive Endpoint**:
   - Always target `GET /api/v1/diagnostics/health` (marked `[AllowAnonymous]`) for external pingers / cron jobs.
   - Never ping `/scalar/v1` or root `/` because zero-trust `FallbackPolicy = RequireAuthenticatedUser()` returns `401 Unauthorized` and never reaches the database.
   - The health endpoint must execute `await dbContext.Database.CanConnectAsync()` to keep the Supabase connection pool active.

2. **Supabase Connection Pooling (Session Mode on Port 5432)**:
   - Supabase Free tier has a 15-connection limit for direct/session connections.
   - Always cap Web API pool size: `Maximum Pool Size=4;Minimum Pool Size=0;Connection Idle Lifetime=15;Timeout=30;`.
   - Always configure `EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: 10s)` in Npgsql DbContext options to survive transient connection resets.

3. **Mobile Client Cold-Start Resilience (.NET MAUI)**:
   - Set client HTTP timeout to at least 90-100 seconds (`ApiConfig.DefaultTimeout = TimeSpan.FromSeconds(100)`).
   - In `DefaultApiClient`, catch `OperationCanceledException when (!ct.IsCancellationRequested)` to handle server cold-start timeouts gracefully without aborting the outbox sync queue.

4. **Sync Logging & Diagnostic Auditing**:
   - NLog is the client logging provider (`NLog.Extensions.Logging` + `NLog.Targets.MauiLog`) and must stay unconditional in all builds (never wrap in `#if DEBUG`).
   - Always attach `X-Correlation-Id` on client requests and echo it back in server responses (`SyncRequestLoggingMiddleware`).
   - Never log `/auth/*` bodies on client or server.
   - Always pass sync payloads and HTTP bodies through `SyncLogRedactor` (truncate to 2KB, mask patient names/phones/notes/tokens/passwords, and mask runs of 7+ digits).
   - Never return `PayloadJson` from diagnostics and audit endpoints (`GetRecentSyncAuditAsync` / `/api/v1/sync/log`).
   - The upload path (`/api/v1/sync/client-logs`) must never feed its own logs back into the upload queue (logged at Debug, excluded from upload rules).

