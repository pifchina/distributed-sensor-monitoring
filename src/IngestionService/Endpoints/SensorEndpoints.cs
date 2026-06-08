using IngestionService.Services;

namespace IngestionService.Endpoints;

public static class SensorEndpoints
{
    public static void MapSensorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sensors/{id}/block", BlockSensorAsync)
            .WithName("BlockSensor")
            .WithTags("Sensors")
            .Produces<BlockSensorResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> BlockSensorAsync(
        string id,
        ISensorPoolService poolService,
        CancellationToken cancellationToken)
    {
        var result = await poolService.BlockSensorAsync(id, cancellationToken);

        return result.Status switch
        {
            BlockSensorStatus.Success => Results.Ok(new BlockSensorResponse(id, result.BlockedUntil!.Value)),
            BlockSensorStatus.NotFound => Results.Problem(
                detail: result.Detail,
                statusCode: StatusCodes.Status404NotFound,
                title: "Sensor not found"),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private sealed record BlockSensorResponse(string SensorId, DateTimeOffset BlockedUntil);
}
