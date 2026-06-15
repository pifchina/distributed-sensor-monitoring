using System.Net.Http.Json;
using SensorMonitoring.Contracts;

namespace IngestionService.Services;

// Best-effort push to the NotificationService. Ingestion must stay fast and must not
// fail just because notifications are unavailable, so failures are logged and swallowed.
public sealed class AlarmNotifier : IAlarmNotifier
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AlarmNotifier> _logger;

    public AlarmNotifier(HttpClient httpClient, ILogger<AlarmNotifier> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task NotifyAsync(AlarmPayload payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/alarms", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "NotificationService rejected alarm from {SensorId} with status {StatusCode}",
                    payload.SensorId,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to push alarm from {SensorId} to NotificationService",
                payload.SensorId);
        }
    }
}
