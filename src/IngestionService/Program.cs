using IngestionService.Configuration;
using IngestionService.Endpoints;
using IngestionService.Services;
using IngestionService.Workers;
using Microsoft.EntityFrameworkCore;
using SensorMonitoring.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SensorDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<FaultToleranceOptions>(
    builder.Configuration.GetSection(FaultToleranceOptions.SectionName));

builder.Services.AddScoped<IReadingIngestionService, ReadingIngestionService>();
builder.Services.AddScoped<ISensorPoolService, SensorPoolService>();
builder.Services.AddHostedService<SensorPoolWorker>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok());
app.MapReadingEndpoints();
app.MapSensorEndpoints();

app.Run();
