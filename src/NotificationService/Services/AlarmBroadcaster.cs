using Microsoft.AspNetCore.SignalR;
using NotificationService.Hubs;
using SensorMonitoring.Contracts;

namespace NotificationService.Services;

public sealed class AlarmBroadcaster : IAlarmBroadcaster
{
    private static readonly object ConsoleLock = new();

    private readonly IHubContext<AlarmsHub> _hubContext;
    private readonly ILogger<AlarmBroadcaster> _logger;

    public AlarmBroadcaster(IHubContext<AlarmsHub> hubContext, ILogger<AlarmBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastAsync(AlarmPayload payload, CancellationToken cancellationToken = default)
    {
        var notification = new AlarmNotification(
            payload.SensorId,
            payload.TriggeringValue,
            payload.Priority,
            AlarmColor.ToWebColor(payload.Priority) ?? "white",
            payload.Timestamp);

        PrintToConsole(notification);

        await _hubContext.Clients.All.SendAsync("AlarmRaised", notification, cancellationToken);

        _logger.LogInformation(
            "Broadcast alarm from {SensorId}: {Value}°C ({Priority})",
            notification.SensorId,
            notification.Value,
            notification.Priority);
    }

    private static void PrintToConsole(AlarmNotification notification)
    {
        var message =
            $"[ALARM] {notification.Timestamp:HH:mm:ss} {notification.SensorId}: " +
            $"{notification.Value:F2}°C ({notification.Priority})";

        var color = AlarmColor.ToConsoleColor(notification.Priority);

        // Serialize console writes so the color swap is not interleaved across alarms.
        lock (ConsoleLock)
        {
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
}
