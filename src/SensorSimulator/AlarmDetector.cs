using SensorMonitoring.Contracts;

namespace SensorSimulator;

public static class AlarmDetector
{
    public static AlarmPriority Detect(double value, double t1, double t2, double t3)
    {
        if (value >= t3)
        {
            return AlarmPriority.Priority3;
        }

        if (value >= t2)
        {
            return AlarmPriority.Priority2;
        }

        if (value >= t1)
        {
            return AlarmPriority.Priority1;
        }

        return AlarmPriority.None;
    }

    public static ConsoleColor? GetConsoleColor(AlarmPriority priority) =>
        AlarmColor.ToConsoleColor(priority);
}
