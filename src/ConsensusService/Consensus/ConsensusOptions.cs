namespace ConsensusService.Consensus;

/// <summary>
/// Tunable parameters for the consensus worker and the outlier detector.
/// Bound from the "Consensus" configuration section.
/// </summary>
public sealed class ConsensusOptions
{
    public const string SectionName = "Consensus";

    /// <summary>
    /// Minimum absolute deviation (in °C) from the population median before a
    /// sensor can be considered an outlier.
    /// </summary>
    public double MinAbsoluteDeviation { get; set; } = 10.0;

    /// <summary>
    /// Modified z-score threshold (based on the median absolute deviation) above
    /// which a sensor is flagged as malicious.
    /// </summary>
    public double MadZScoreThreshold { get; set; } = 2.5;

    /// <summary>
    /// Minimum number of reporting sensors required before outlier detection runs.
    /// Below this, consensus is still computed but no sensor is flagged.
    /// </summary>
    public int MinSensorsForDetection { get; set; } = 3;

    /// <summary>
    /// Grace period (in seconds) added after each minute boundary before the
    /// just-completed window is processed, tolerating slightly late readings.
    /// </summary>
    public int GraceSeconds { get; set; } = 5;
}
