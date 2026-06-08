namespace IngestionService.Services;

public interface ISensorPoolService
{
    Task<BlockSensorResult> BlockSensorAsync(string sensorId, CancellationToken cancellationToken = default);

    Task MaintainPoolAsync(CancellationToken cancellationToken = default);
}

public enum BlockSensorStatus
{
    Success,
    NotFound
}

public sealed record BlockSensorResult(BlockSensorStatus Status, DateTimeOffset? BlockedUntil = null, string? Detail = null);
