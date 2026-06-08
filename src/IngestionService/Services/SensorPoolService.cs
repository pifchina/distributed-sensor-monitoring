using IngestionService.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SensorMonitoring.Data;

namespace IngestionService.Services;

public sealed class SensorPoolService : ISensorPoolService
{
    private const int TargetActiveCount = 5;
    private const int BlockDurationSeconds = 30;

    private readonly SensorDbContext _dbContext;
    private readonly FaultToleranceOptions _options;
    private readonly ILogger<SensorPoolService> _logger;

    public SensorPoolService(
        SensorDbContext dbContext,
        IOptions<FaultToleranceOptions> options,
        ILogger<SensorPoolService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BlockSensorResult> BlockSensorAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        var sensor = await _dbContext.Sensors
            .FirstOrDefaultAsync(s => s.Id == sensorId, cancellationToken);

        if (sensor is null)
        {
            return new BlockSensorResult(BlockSensorStatus.NotFound, Detail: $"Sensor '{sensorId}' was not found.");
        }

        var blockedUntil = DateTimeOffset.UtcNow.AddSeconds(BlockDurationSeconds);
        sensor.IsBlockedUntil = blockedUntil;

        if (sensor.IsActive)
        {
            sensor.IsActive = false;
            _logger.LogInformation(
                "Sensor {SensorId} blocked until {BlockedUntil:O} and deactivated",
                sensorId,
                blockedUntil);
        }
        else
        {
            _logger.LogInformation(
                "Sensor {SensorId} blocked until {BlockedUntil:O}",
                sensorId,
                blockedUntil);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new BlockSensorResult(BlockSensorStatus.Success, blockedUntil);
    }

    public Task MaintainPoolAsync(CancellationToken cancellationToken = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var utcNow = DateTimeOffset.UtcNow;
            var inactivityCutoff = utcNow.AddSeconds(-_options.InactivityTimeoutSeconds);

            var timedOutSensors = await _dbContext.Sensors
                .Where(s => s.IsActive &&
                            s.LastMessageAt != null &&
                            s.LastMessageAt < inactivityCutoff)
                .ToListAsync(cancellationToken);

            foreach (var sensor in timedOutSensors)
            {
                sensor.IsActive = false;
                _logger.LogWarning(
                    "Sensor {SensorId} marked inactive due to inactivity (last message: {LastMessageAt})",
                    sensor.Id,
                    sensor.LastMessageAt!.Value.ToString("O"));
            }

            var activeCount = await CountEligibleActiveSensorsAsync(utcNow, cancellationToken);

            while (activeCount < TargetActiveCount)
            {
                var standby = await _dbContext.Sensors
                    .Where(s => !s.IsActive &&
                                (s.IsBlockedUntil == null || s.IsBlockedUntil <= utcNow))
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (standby is null)
                {
                    _logger.LogWarning(
                        "No standby sensors available to reach target of {TargetActiveCount} active sensors (current: {ActiveCount})",
                        TargetActiveCount,
                        activeCount);
                    break;
                }

                standby.IsActive = true;
                activeCount++;

                _logger.LogInformation(
                    "Promoted standby sensor {SensorId} to active ({ActiveCount}/{TargetActiveCount})",
                    standby.Id,
                    activeCount,
                    TargetActiveCount);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private Task<int> CountEligibleActiveSensorsAsync(DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        return _dbContext.Sensors
            .CountAsync(
                s => s.IsActive &&
                     (s.IsBlockedUntil == null || s.IsBlockedUntil <= utcNow),
                cancellationToken);
    }
}
