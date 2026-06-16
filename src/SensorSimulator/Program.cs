using Microsoft.Extensions.Configuration;
using SensorMonitoring.Security;
using SensorSimulator;

if (args.Contains("--keygen"))
{
    KeyGenerator.Generate(ParseOption(args, "--output") ?? "keys");
    return 0;
}

var sensorId = ParseOption(args, "--sensor-id", "-s");
if (sensorId is null)
{
    Console.Error.WriteLine("Usage: dotnet run -- --sensor-id SENSOR-001 [--malicious] [--bad-signature] [--replay] [--flood]");
    Console.Error.WriteLine("       dotnet run -- --keygen [--output keys]");
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
    .AddEnvironmentVariables()
    .Build();

var options = configuration.Get<SimulatorOptions>() ?? new SimulatorOptions();
var ingestionBaseUrl = options.IngestionBaseUrl.TrimEnd('/') + "/";


var sensorPrivateKeyPath = Path.Combine(
    options.SensorPrivateKeysDirectory,
    $"{sensorId}{PemKeyLoader.PrivateKeySuffix}");

if (!File.Exists(options.ServerPublicKeyPath) || !File.Exists(sensorPrivateKeyPath))
{
    Console.Error.WriteLine($"Missing key files ({options.ServerPublicKeyPath}, {sensorPrivateKeyPath}).");
    Console.Error.WriteLine("Generate them first: dotnet run --project src/SensorSimulator -- --keygen");
    return 1;
}

using var protector = new SensorMessageProtector(
    PemKeyLoader.LoadRsa(options.ServerPublicKeyPath),
    PemKeyLoader.LoadEcdsa(sensorPrivateKeyPath));

var modes = new SimulatorModes(
    Malicious: args.Contains("--malicious"),
    BadSignature: args.Contains("--bad-signature"),
    Replay: args.Contains("--replay"),
    Flood: args.Contains("--flood"));

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(ingestionBaseUrl)
};

var client = new SensorClient(httpClient, sensor, protector, modes);
using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"[{sensor.Id}] Starting simulator - sending to {ingestionBaseUrl}readings");
Console.WriteLine($"[{sensor.Id}] Temperature range: [{sensor.TemperatureMin}, {sensor.TemperatureMax}]°C");
Console.WriteLine($"[{sensor.Id}] Messages are AES-256-GCM encrypted and ECDSA-signed.");

if (modes.Malicious)
{
    WriteBanner($"[{sensor.Id}] MALICIOUS MODE: sending pinned outlier values ({sensor.TemperatureMax - 0.5}°C)");
}

if (modes.BadSignature)
{
    WriteBanner($"[{sensor.Id}] BAD-SIGNATURE MODE: corrupting every signature (server should reject with 401)");
}

if (modes.Replay)
{
    WriteBanner($"[{sensor.Id}] REPLAY MODE: sending every envelope twice (server should reject the duplicate with 409)");
}

if (modes.Flood)
{
    WriteBanner($"[{sensor.Id}] FLOOD MODE: sending ~20 msg/s (server should rate-limit with 429 and block the sensor)");
}

Console.WriteLine("Press Ctrl+C to stop.");

while (!cts.Token.IsCancellationRequested)
{
    await client.SendReadingAsync(cts.Token);

    var delayMs = modes.Flood ? 50 : Random.Shared.Next(1000, 10000);
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

static string? ParseOption(string[] args, params string[] names)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (names.Contains(args[i]) && i + 1 < args.Length)
        {
            return args[i + 1];
        }
    }

    return null;
}

static void WriteBanner(string message)
{
    var previous = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(message);
    Console.ForegroundColor = previous;
}