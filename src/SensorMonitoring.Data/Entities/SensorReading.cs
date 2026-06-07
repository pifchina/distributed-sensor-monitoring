using SensorMonitoring.Contracts;

namespace SensorMonitoring.Data.Entities;

public class SensorReading
{
    public long Id { get; set; }

    public string SensorId { get; set; } = null!;

    public double Value { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public bool IsConsensus { get; set; }

    public AlarmPriority AlarmPriority { get; set; }

    public Sensor Sensor { get; set; } = null!;
}
