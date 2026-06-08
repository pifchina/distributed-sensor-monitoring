using SensorMonitoring.Contracts;

namespace SensorSimulator;

public sealed class SimulatorOptions
{
    public string IngestionBaseUrl { get; set; } = "http://localhost:5001";
}

public sealed record SensorMetadata(
    string Id,
    double TemperatureMin,
    double TemperatureMax,
    DataQuality DataQuality,
    double AlarmThreshold1,
    double AlarmThreshold2,
    double AlarmThreshold3);

public static class SensorConfig
{
    public static IReadOnlyDictionary<string, SensorMetadata> Sensors { get; } =
        new Dictionary<string, SensorMetadata>(StringComparer.Ordinal)
        {
            ["SENSOR-001"] = new("SENSOR-001", -10, 35, DataQuality.Good, 30, 32, 34),
            ["SENSOR-002"] = new("SENSOR-002", 0, 40, DataQuality.Good, 35, 37, 39),
            ["SENSOR-003"] = new("SENSOR-003", 5, 45, DataQuality.Good, 38, 41, 44),
            ["SENSOR-004"] = new("SENSOR-004", -5, 30, DataQuality.Good, 25, 27, 29),
            ["SENSOR-005"] = new("SENSOR-005", 10, 50, DataQuality.Good, 42, 46, 49),
            ["SENSOR-006"] = new("SENSOR-006", -15, 25, DataQuality.Good, 20, 22, 24),
            ["SENSOR-007"] = new("SENSOR-007", 15, 55, DataQuality.Good, 48, 51, 54),
        };

    public static bool TryGetSensor(string sensorId, out SensorMetadata metadata) =>
        Sensors.TryGetValue(sensorId, out metadata!);
}
