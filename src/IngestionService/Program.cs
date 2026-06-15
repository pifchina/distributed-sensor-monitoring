using IngestionService.Configuration;
using IngestionService.Endpoints;
using IngestionService.Services;
using IngestionService.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SensorMonitoring.Data;
using SensorMonitoring.Security;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SensorDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<FaultToleranceOptions>(
    builder.Configuration.GetSection(FaultToleranceOptions.SectionName));
builder.Services.Configure<SecurityOptions>(
    builder.Configuration.GetSection(SecurityOptions.SectionName));


builder.Services.AddSingleton<ISecureEnvelopeOpener>(provider =>
{
    var security = provider.GetRequiredService<IOptions<SecurityOptions>>().Value;
    var contentRoot = provider.GetRequiredService<IHostEnvironment>().ContentRootPath;
    return new SecureEnvelopeOpener(
        PemKeyLoader.LoadRsa(Path.Combine(contentRoot, security.ServerPrivateKeyPath)),
        PemKeyLoader.LoadSensorPublicKeys(Path.Combine(contentRoot, security.SensorPublicKeysDirectory)));
});
builder.Services.AddScoped<IReadingIngestionService, ReadingIngestionService>();
builder.Services.AddScoped<ISensorPoolService, SensorPoolService>();

var notificationBaseUrl = builder.Configuration["NotificationService:BaseUrl"]
    ?? "http://localhost:5003";
builder.Services.AddHttpClient<IAlarmNotifier, AlarmNotifier>(client =>
{
    client.BaseAddress = new Uri(notificationBaseUrl);
});
builder.Services.AddSingleton<SensorBlockCoordinator>();
builder.Services.AddHostedService<SensorPoolWorker>();

var rateLimiting = builder.Configuration
    .GetSection(RateLimitingOptions.SectionName)
    .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    limiter.AddPolicy("per-sensor", httpContext =>
    {
        var sensorId = httpContext.Request.Headers["X-Sensor-Id"].FirstOrDefault()
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(sensorId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimiting.PermitLimit,
            Window = TimeSpan.FromSeconds(rateLimiting.WindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    limiter.OnRejected = async (context, cancellationToken) =>
    {
        var httpContext = context.HttpContext;
        var sensorId = httpContext.Request.Headers["X-Sensor-Id"].FirstOrDefault();
        if (sensorId is null)
        {
            return;
        }

        // Temporary block at most once per debounce window, not
        // on every rejected flood request.
        var coordinator = httpContext.RequestServices.GetRequiredService<SensorBlockCoordinator>();
        if (coordinator.ShouldBlock(sensorId))
        {
            var logger = httpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("RateLimiter");
            logger.LogWarning(
                "Sensor {SensorId} exceeded {PermitLimit} requests per {WindowSeconds}s - applying temporary block",
                sensorId,
                rateLimiting.PermitLimit,
                rateLimiting.WindowSeconds);

            var poolService = httpContext.RequestServices.GetRequiredService<ISensorPoolService>();
            await poolService.BlockSensorAsync(sensorId, cancellationToken);
        }
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

_ = app.Services.GetRequiredService<ISecureEnvelopeOpener>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok());
app.MapReadingEndpoints();
app.MapSensorEndpoints();

app.Run();
