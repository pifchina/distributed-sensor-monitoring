using System.Net.Http.Json;
using SensorMonitoring.Contracts;
using SensorMonitoring.Security;

namespace SensorSimulator;

public sealed class SensorClient
{
    private readonly HttpClient _httpClient;
    private readonly SensorMetadata _sensor;
    private readonly ISensorMessageProtector _protector;
    private readonly SimulatorModes _modes;
    private long _messageId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public SensorClient(
        HttpClient httpClient,
        SensorMetadata sensor,
        ISensorMessageProtector protector,
        SimulatorModes modes)
    {
        _httpClient = httpClient;
        _sensor = sensor;
        _protector = protector;
        _modes = modes;
        _httpClient.DefaultRequestHeaders.Add("X-Sensor-Id", sensor.Id);
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

        var envelope = _protector.Protect(message);

        if (_modes.BadSignature)
        {
            envelope = CorruptSignature(envelope);
        }

        await PostEnvelopeAsync(envelope, message, temperature, alarmPriority, cancellationToken);

        if (_modes.Replay)
        {
            await PostEnvelopeAsync(envelope, message, temperature, alarmPriority, cancellationToken, replayed: true);
        }
    }

    private async Task PostEnvelopeAsync(
        SecureEnvelope envelope,
        SensorMessage message,
        double temperature,
        AlarmPriority alarmPriority,
        CancellationToken cancellationToken,
        bool replayed = false)
    {
        var prefix = replayed
            ? $"[{_sensor.Id}] Replayed reading #{message.MessageId}"
            : $"[{_sensor.Id}] Sent reading #{message.MessageId}";

        try
        {
            var response = await _httpClient.PostAsJsonAsync("readings", envelope, cancellationToken);
            var statusCode = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
            {
                WriteLine($"{prefix}: {temperature:F2}°C - OK ({statusCode})", alarmPriority);
                return;
            }
            else
            {
                var reason = statusCode switch
                {
                    401 => "REJECTED: invalid signature",
                    409 => "REJECTED: replay/blocked",
                    429 => "REJECTED: rate limited",
                    _ => "FAILED"
                };
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                WriteLine($"{prefix}: {temperature:F2}°C - {reason} ({statusCode}) {body}", alarmPriority);
            }
        
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            WriteLine($"{prefix}: {temperature:F2}°C - ERROR: {ex.Message}", alarmPriority);
        }
    }

    private static SecureEnvelope CorruptSignature(SecureEnvelope envelope)
    {
        var signature = Convert.FromBase64String(envelope.Signature);
        signature[0] ^= 0xFF;
        return envelope with { Signature = Convert.ToBase64String(signature) };
    }

    private double GenerateTemperature()
    {
        // A malicious sensor consistently reports near its maximum, producing a
        // clean statistical outlier the consensus service should detect.
        if (_modes.Malicious)
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
