using System.Threading.Channels;
using ClinicManagementApp.Api.Services.Interfaces;

namespace ClinicManagementApp.Api.Services.Implementations;

/// <summary>
/// In-process Channel-based notifier providing sub-millisecond wakeup signals between publisher and consumer.
/// </summary>
public class PatientOutboxQueueNotifier : IPatientOutboxQueueNotifier
{
    private readonly Channel<byte> _channel = Channel.CreateBounded<byte>(new BoundedChannelOptions(10)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = false,
        SingleWriter = false
    });

    public void Notify()
    {
        _channel.Writer.TryWrite(1);
    }

    public async ValueTask<bool> WaitForNotificationAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await _channel.Reader.ReadAsync(cts.Token);
            // Drain any pending buffered tokens
            while (_channel.Reader.TryRead(out _)) { }
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
