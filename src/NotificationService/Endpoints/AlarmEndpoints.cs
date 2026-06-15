using NotificationService.Services;
using SensorMonitoring.Contracts;

namespace NotificationService.Endpoints;

public static class AlarmEndpoints
{
    public static void MapAlarmEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/alarms", RaiseAlarmAsync)
            .WithName("RaiseAlarm")
            .WithTags("Alarms")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> RaiseAlarmAsync(
        AlarmPayload payload,
        IAlarmBroadcaster broadcaster,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.SensorId))
        {
            return Results.Problem(
                detail: "SensorId is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid alarm payload");
        }

        await broadcaster.BroadcastAsync(payload, cancellationToken);

        return Results.Accepted();
    }
}
