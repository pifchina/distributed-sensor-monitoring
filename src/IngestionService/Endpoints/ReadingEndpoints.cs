using IngestionService.Services;
using SensorMonitoring.Contracts;
using SensorMonitoring.Security;

namespace IngestionService.Endpoints;

public static class ReadingEndpoints
{
    public static void MapReadingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/readings", IngestReadingAsync)
            .WithName("IngestReading")
            .WithTags("Readings")
            .RequireRateLimiting("per-sensor")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static async Task<IResult> IngestReadingAsync(
        SecureEnvelope envelope,
        HttpContext httpContext,
        ISecureEnvelopeOpener envelopeOpener,
        IReadingIngestionService ingestionService,
        CancellationToken cancellationToken)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Sensor-Id", out var headerSensorId) &&
            headerSensorId != envelope.SensorId)
        {
            return Results.Problem(
                detail: "X-Sensor-Id header does not match the envelope sensor ID.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid envelope");
        }

        var openResult = envelopeOpener.Open(envelope);
        switch (openResult.Status)
        {
            case EnvelopeOpenStatus.UnknownSensorKey:
            case EnvelopeOpenStatus.InvalidSignature:
                return Results.Problem(
                    detail: $"Signature verification failed for sensor '{envelope.SensorId}'.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid signature");
            case EnvelopeOpenStatus.DecryptionFailed:
            case EnvelopeOpenStatus.MalformedEnvelope:
                return Results.Problem(
                    detail: "The envelope could not be decrypted.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid envelope");
        }

        var message = openResult.Message!;
        if (message.SensorId != envelope.SensorId)
        {
            return Results.Problem(
                detail: "Envelope sensor ID does not match the encrypted message.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid envelope");
        }

        var result = await ingestionService.IngestAsync(message, cancellationToken);

        return result.Status switch
        {
            IngestionStatus.Accepted => Results.Accepted(),
            IngestionStatus.NotFound => Results.Problem(
                detail: result.Detail,
                statusCode: StatusCodes.Status404NotFound,
                title: "Sensor not found"),
            IngestionStatus.BadRequest => Results.Problem(
                detail: result.Detail,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid reading"),
            IngestionStatus.Conflict => Results.Problem(
                detail: result.Detail,
                statusCode: StatusCodes.Status409Conflict,
                title: "Sensor unavailable"),
            IngestionStatus.Replay => Results.Problem(
                detail: result.Detail,
                statusCode: StatusCodes.Status409Conflict,
                title: "Replay detected"),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
