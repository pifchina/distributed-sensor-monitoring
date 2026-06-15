namespace SensorMonitoring.Contracts;

public static class AlarmColor
{
    public static ConsoleColor? ToConsoleColor(AlarmPriority priority) => priority switch
    {
        AlarmPriority.Priority1 => ConsoleColor.Yellow,
        AlarmPriority.Priority2 => ConsoleColor.DarkYellow,
        AlarmPriority.Priority3 => ConsoleColor.Red,
        _ => null
    };

    public static string? ToWebColor(AlarmPriority priority) => priority switch
    {
        AlarmPriority.Priority1 => "yellow",
        AlarmPriority.Priority2 => "orange",
        AlarmPriority.Priority3 => "red",
        _ => null
    };
}
