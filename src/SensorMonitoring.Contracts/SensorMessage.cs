namespace SensorMonitoring.Contracts;

public record SensorMessage(
    string SensorId,
    double TemperatureValue,
    DateTimeOffset Timestamp,
    long MessageId,
    DataQuality DataQuality,
    AlarmPriority? AlarmPriority = null);
