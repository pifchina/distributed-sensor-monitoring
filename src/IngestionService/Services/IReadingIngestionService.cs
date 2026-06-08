using SensorMonitoring.Contracts;

namespace IngestionService.Services;

public interface IReadingIngestionService
{
    Task<IngestionResult> IngestAsync(SensorMessage message, CancellationToken cancellationToken = default);
}

public enum IngestionStatus
{
    Accepted,
    NotFound,
    BadRequest,
    Conflict
}

public sealed record IngestionResult(IngestionStatus Status, string? Detail = null);
