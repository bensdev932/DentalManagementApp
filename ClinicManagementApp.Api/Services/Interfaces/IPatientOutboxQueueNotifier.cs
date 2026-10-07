namespace ClinicManagementApp.Api.Services.Interfaces;

/// <summary>
/// Sub-millisecond signaling channel between patient publishers and background outbox processor.
/// </summary>
public interface IPatientOutboxQueueNotifier
{
    /// <summary>
    /// Signals the background processor that a new outbox task is available.
    /// </summary>
    void Notify();

    /// <summary>
    /// Asynchronously waits for a notification signal or times out.
    /// </summary>
    /// <param name="timeout">Maximum duration to wait for a signal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if signaled, false if timed out.</returns>
    ValueTask<bool> WaitForNotificationAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
