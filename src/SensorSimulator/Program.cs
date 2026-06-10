using Microsoft.Extensions.Configuration;
using SensorSimulator;

var sensorId = ParseSensorId(args);
if (sensorId is null)
{
    Console.Error.WriteLine("Usage: dotnet run -- --sensor-id SENSOR-001");
    return 1;
}

if (!SensorConfig.TryGetSensor(sensorId, out var sensor))
{
    Console.Error.WriteLine($"Unknown sensor ID: {sensorId}");
    Console.Error.WriteLine($"Valid IDs: {string.Join(", ", SensorConfig.Sensors.Keys)}");
    return 1;
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var options = configuration.Get<SimulatorOptions>() ?? new SimulatorOptions();
var ingestionBaseUrl = options.IngestionBaseUrl.TrimEnd('/');

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(ingestionBaseUrl)
};

var malicious = ParseMalicious(args);
var client = new SensorClient(httpClient, sensor, malicious);
using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"[{sensor.Id}] Starting simulator - sending to {ingestionBaseUrl}/api/readings");
Console.WriteLine($"[{sensor.Id}] Temperature range: [{sensor.TemperatureMin}, {sensor.TemperatureMax}]°C");

if (malicious)
{
    var previous = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[{sensor.Id}] MALICIOUS MODE: sending pinned outlier values ({sensor.TemperatureMax - 0.5}°C)");
    Console.ForegroundColor = previous;
}

Console.WriteLine("Press Ctrl+C to stop.");

while (!cts.Token.IsCancellationRequested)
{
    await client.SendReadingAsync(cts.Token);

    var delayMs = Random.Shared.Next(1000, 10000);
    try
    {
        await Task.Delay(delayMs, cts.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

Console.WriteLine($"[{sensor.Id}] Stopped.");
return 0;

static string? ParseSensorId(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] is "--sensor-id" or "-s" && i + 1 < args.Length)
        {
            return args[i + 1];
        }
    }

    return null;
}

static bool ParseMalicious(string[] args) => args.Contains("--malicious");
