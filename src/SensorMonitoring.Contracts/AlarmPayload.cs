namespace SensorMonitoring.Contracts;

public record AlarmPayload(
    string SensorId,
    double TriggeringValue,
    AlarmPriority Priority,
    DateTimeOffset Timestamp);
