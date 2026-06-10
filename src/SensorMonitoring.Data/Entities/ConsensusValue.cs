namespace SensorMonitoring.Data.Entities;

public class ConsensusValue
{
    public long Id { get; set; }

    public double CalculatedValue { get; set; }

    public DateTimeOffset PeriodStart { get; set; }

    public DateTimeOffset PeriodEnd { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public int SensorCount { get; set; }

    public int SampleCount { get; set; }
}
