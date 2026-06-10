using System.Net.Http.Json;
using SensorMonitoring.Contracts;

namespace SensorSimulator;

public sealed class SensorClient
{
    private readonly HttpClient _httpClient;
    private readonly SensorMetadata _sensor;
    private readonly bool _malicious;
    private long _messageId = 1;

    public SensorClient(HttpClient httpClient, SensorMetadata sensor, bool malicious = false)
    {
        _httpClient = httpClient;
        _sensor = sensor;
        _malicious = malicious;
    }

    public async Task SendReadingAsync(CancellationToken cancellationToken)
    {
        var temperature = GenerateTemperature();
        var alarmPriority = AlarmDetector.Detect(
            temperature,
            _sensor.AlarmThreshold1,
            _sensor.AlarmThreshold2,
            _sensor.AlarmThreshold3);

        var message = new SensorMessage(
            _sensor.Id,
            temperature,
            DateTimeOffset.UtcNow,
            _messageId++,
            _sensor.DataQuality,
            alarmPriority == AlarmPriority.None ? null : alarmPriority);

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/readings", message, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                WriteLine(
                    $"[{_sensor.Id}] Sent reading #{message.MessageId}: {temperature:F2}°C - OK ({(int)response.StatusCode})",
                    alarmPriority);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                WriteLine(
                    $"[{_sensor.Id}] Sent reading #{message.MessageId}: {temperature:F2}°C - FAILED ({(int)response.StatusCode}) {body}",
                    alarmPriority);
            }
        }
        catch (Exception ex)
        {
            WriteLine(
                $"[{_sensor.Id}] Sent reading #{message.MessageId}: {temperature:F2}°C - ERROR: {ex.Message}",
                alarmPriority);
        }
    }

    private double GenerateTemperature()
    {
        // A malicious sensor consistently reports near its maximum, producing a
        // clean statistical outlier the consensus service should detect.
        if (_malicious)
        {
            return _sensor.TemperatureMax - 0.5;
        }

        if (Random.Shared.NextDouble() < 0.05)
        {
            var alarmMin = _sensor.AlarmThreshold1;
            var alarmMax = _sensor.TemperatureMax;
            if (alarmMin < alarmMax)
            {
                return alarmMin + Random.Shared.NextDouble() * (alarmMax - alarmMin);
            }
        }

        var range = _sensor.TemperatureMax - _sensor.TemperatureMin;
        return _sensor.TemperatureMin + Random.Shared.NextDouble() * range;
    }

    private static void WriteLine(string message, AlarmPriority alarmPriority)
    {
        var color = AlarmDetector.GetConsoleColor(alarmPriority);
        if (color is null)
        {
            Console.WriteLine(message);
            return;
        }

        var previous = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color.Value;
            Console.WriteLine(message);
        }
        finally
        {
            Console.ForegroundColor = previous;
        }
    }
}
