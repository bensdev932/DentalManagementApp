using ClinicManagementApp.Api.Services.Interfaces;

namespace ClinicManagementApp.Api.Services.Implementations;

public class ClientLogRetentionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ClientLogRetentionService> _logger;

    public ClientLogRetentionService(IServiceProvider serviceProvider, ILogger<ClientLogRetentionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await PurgeOldLogsAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Client log retention service encountered an unexpected error.");
        }
    }

    private async Task PurgeOldLogsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var logService = scope.ServiceProvider.GetRequiredService<IClientLogService>();
            var cutoff = DateTime.UtcNow.AddDays(-14);
            int purged = await logService.PurgeAsync(cutoff, ct);
            if (purged > 0)
            {
                _logger.LogInformation("Purged {PurgedCount} client log entries older than 14 days.", purged);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during client log retention purge.");
        }
    }
}

