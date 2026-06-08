namespace IngestionService.Configuration;

public sealed class FaultToleranceOptions
{
    public const string SectionName = "FaultTolerance";

    public int InactivityTimeoutSeconds { get; set; } = 10;
}
