using SensorMonitoring.Contracts;

namespace IngestionService.Services;

public interface IAlarmNotifier
{
    Task NotifyAsync(AlarmPayload payload, CancellationToken cancellationToken = default);
}
