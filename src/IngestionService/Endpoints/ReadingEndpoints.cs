using IngestionService.Services;
using SensorMonitoring.Contracts;

namespace IngestionService.Endpoints;

public static class ReadingEndpoints
{
    public static void MapReadingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/readings", IngestReadingAsync)
            .WithName("IngestReading")
            .WithTags("Readings")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> IngestReadingAsync(
        SensorMessage message,
        IReadingIngestionService ingestionService,
        CancellationToken cancellationToken)
    {
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
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
