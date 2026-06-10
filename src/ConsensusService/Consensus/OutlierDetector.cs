namespace ConsensusService.Consensus;

/// <summary>
/// The aggregated reading of a single sensor over a consensus window.
/// </summary>
public sealed record SensorAggregate(string SensorId, double Median, int SampleCount);

/// <summary>
/// The outcome of outlier detection: the honest sensors whose readings feed the
/// consensus value, the sensors flagged as malicious, and whether the majority
/// guard suppressed flagging.
/// </summary>
public sealed record OutlierDetectionResult(
    IReadOnlyList<SensorAggregate> Honest,
    IReadOnlyList<SensorAggregate> Outliers,
    bool MajorityGuardTriggered);

/// <summary>
/// Simplified Byzantine-fault-tolerant aggregation: a robust median combined with
/// median-absolute-deviation (MAD) outlier detection. With a trusted aggregator
/// the median tolerates up to floor((n-1)/2) arbitrarily-lying sensors.
/// </summary>
public static class OutlierDetector
{
    // Consistency constant making the modified z-score comparable to a standard
    // deviation for normally distributed data (1 / 0.6745 ≈ 1.4826).
    private const double MadScale = 0.6745;

    public static OutlierDetectionResult Detect(
        IReadOnlyList<SensorAggregate> sensors,
        ConsensusOptions options)
    {
        // Too few sensors to judge anyone — treat everyone as honest.
        if (sensors.Count < options.MinSensorsForDetection)
        {
            return new OutlierDetectionResult(sensors, Array.Empty<SensorAggregate>(), false);
        }

        var populationMedian = Median(sensors.Select(s => s.Median).ToList());
        var deviations = sensors.Select(s => Math.Abs(s.Median - populationMedian)).ToList();
        var mad = Median(deviations);

        var outliers = sensors.Where(s =>
        {
            var deviation = Math.Abs(s.Median - populationMedian);
            if (deviation < options.MinAbsoluteDeviation)
            {
                return false;
            }

            // When MAD is zero the z-score is undefined; the absolute rule alone decides.
            if (mad == 0)
            {
                return true;
            }

            var zScore = MadScale * deviation / mad;
            return zScore >= options.MadZScoreThreshold;
        }).ToList();

        // Majority guard: the median assumption holds only while fewer than half the
        // sensors lie. If the rule would flag a majority, trust no flag this round.
        if (outliers.Count * 2 >= sensors.Count)
        {
            return new OutlierDetectionResult(sensors, Array.Empty<SensorAggregate>(), true);
        }

        var honest = sensors.Where(s => !outliers.Contains(s)).ToList();
        return new OutlierDetectionResult(honest, outliers, false);
    }

    /// <summary>
    /// Returns the median of the values. For an even count, returns the average of
    /// the two middle values. The input must be non-empty.
    /// </summary>
    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            throw new ArgumentException("Cannot compute the median of an empty sequence.", nameof(values));
        }

        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;

        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
