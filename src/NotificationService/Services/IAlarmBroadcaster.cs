using SensorMonitoring.Contracts;

namespace NotificationService.Services;

public interface IAlarmBroadcaster
{
    Task BroadcastAsync(AlarmPayload payload, CancellationToken cancellationToken = default);
}
