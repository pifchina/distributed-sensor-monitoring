using Microsoft.EntityFrameworkCore;
using SensorMonitoring.Contracts;
using SensorMonitoring.Data;
using SensorMonitoring.Data.Entities;

namespace IngestionService.Services;

public sealed class ReadingIngestionService : IReadingIngestionService
{
    private readonly SensorDbContext _dbContext;
    private readonly ILogger<ReadingIngestionService> _logger;

    public ReadingIngestionService(SensorDbContext dbContext, ILogger<ReadingIngestionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestAsync(SensorMessage message, CancellationToken cancellationToken = default)
    {
        if (message.MessageId <= 0)
        {
            return new IngestionResult(IngestionStatus.BadRequest, "MessageId must be greater than zero.");
        }

        var sensor = await _dbContext.Sensors
            .FirstOrDefaultAsync(s => s.Id == message.SensorId, cancellationToken);

        if (sensor is null)
        {
            return new IngestionResult(IngestionStatus.NotFound, $"Sensor '{message.SensorId}' was not found.");
        }

        if (!sensor.IsActive)
        {
            return new IngestionResult(IngestionStatus.Conflict, $"Sensor '{message.SensorId}' is not active.");
        }

        var utcNow = DateTimeOffset.UtcNow;
        if (sensor.IsBlockedUntil is { } blockedUntil && blockedUntil > utcNow)
        {
            return new IngestionResult(
                IngestionStatus.Conflict,
                $"Sensor '{message.SensorId}' is blocked until {blockedUntil:O}.");
        }

        if (message.TemperatureValue < sensor.TemperatureMin || message.TemperatureValue > sensor.TemperatureMax)
        {
            return new IngestionResult(
                IngestionStatus.BadRequest,
                $"Temperature {message.TemperatureValue} is outside the allowed range [{sensor.TemperatureMin}, {sensor.TemperatureMax}].");
        }

        var alarmPriority = message.AlarmPriority ?? AlarmPriority.None;

        _dbContext.SensorReadings.Add(new SensorReading
        {
            SensorId = message.SensorId,
            Value = message.TemperatureValue,
            Timestamp = message.Timestamp,
            IsConsensus = false,
            AlarmPriority = alarmPriority
        });

        if (alarmPriority != AlarmPriority.None)
        {
            _dbContext.AlarmEvents.Add(new AlarmEvent
            {
                SensorId = message.SensorId,
                Value = message.TemperatureValue,
                Priority = alarmPriority,
                Timestamp = message.Timestamp
            });
        }

        sensor.LastMessageAt = message.Timestamp;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Ingested reading #{MessageId} from {SensorId}: {Value}°C (alarm: {AlarmPriority})",
            message.MessageId,
            message.SensorId,
            message.TemperatureValue,
            alarmPriority);

        return new IngestionResult(IngestionStatus.Accepted);
    }
}
