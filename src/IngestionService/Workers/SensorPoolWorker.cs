using IngestionService.Services;

namespace IngestionService.Workers;

public sealed class SensorPoolWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SensorPoolWorker> _logger;

    public SensorPoolWorker(IServiceScopeFactory scopeFactory, ILogger<SensorPoolWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sensor pool worker started (poll interval: {PollInterval}s)", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var poolService = scope.ServiceProvider.GetRequiredService<ISensorPoolService>();
                await poolService.MaintainPoolAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error maintaining sensor pool");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        _logger.LogInformation("Sensor pool worker stopped");
    }
}
