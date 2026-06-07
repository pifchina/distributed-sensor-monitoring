using SensorMonitoring.Contracts;

namespace SensorMonitoring.Data.Entities;

public class Sensor
{
    public string Id { get; set; } = null!;

    public double TemperatureMin { get; set; }

    public double TemperatureMax { get; set; }

    public DataQuality DataQuality { get; set; }

    public double AlarmThreshold1 { get; set; }

    public double AlarmThreshold2 { get; set; }

    public double AlarmThreshold3 { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset? LastMessageAt { get; set; }

    public DateTimeOffset? IsBlockedUntil { get; set; }

    public ICollection<SensorReading> Readings { get; set; } = [];

    public ICollection<AlarmEvent> AlarmEvents { get; set; } = [];
}
