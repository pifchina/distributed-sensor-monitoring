namespace SensorMonitoring.Contracts;

public record AlarmNotification(
    string SensorId,
    double Value,
    AlarmPriority Priority,
    string Color,
    DateTimeOffset Timestamp);
