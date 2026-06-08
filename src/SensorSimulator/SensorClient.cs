using System.Net.Http.Json;
using SensorMonitoring.Contracts;

namespace SensorSimulator;

public sealed class SensorClient
{
    private readonly HttpClient _httpClient;
    private readonly SensorMetadata _sensor;
    private long _messageId = 1;

    public SensorClient(HttpClient httpClient, SensorMetadata sensor)
    {
        _httpClient = httpClient;
        _sensor = sensor;
    }

    public async Task SendReadingAsync(CancellationToken cancellationToken)
    {
        var temperature = GenerateTemperature();
        var message = new SensorMessage(
            _sensor.Id,
            temperature,
            DateTimeOffset.UtcNow,
            _messageId++,
            _sensor.DataQuality);

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/readings", message, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"[{_sensor.Id}] Sent reading #{message.MessageId}: {temperature:F2}°C — OK ({(int)response.StatusCode})");
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine(
                    $"[{_sensor.Id}] Sent reading #{message.MessageId}: {temperature:F2}°C — FAILED ({(int)response.StatusCode}) {body}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[{_sensor.Id}] Sent reading #{message.MessageId}: {temperature:F2}°C — ERROR: {ex.Message}");
        }
    }

    private double GenerateTemperature()
    {
        var range = _sensor.TemperatureMax - _sensor.TemperatureMin;
        return _sensor.TemperatureMin + Random.Shared.NextDouble() * range;
    }
}
