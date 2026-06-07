using SensorMonitoring.Contracts;

namespace SensorMonitoring.Data.Entities;

public class AlarmEvent
{
    public long Id { get; set; }

    public string SensorId { get; set; } = null!;

    public double Value { get; set; }

    public AlarmPriority Priority { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public Sensor Sensor { get; set; } = null!;
}
