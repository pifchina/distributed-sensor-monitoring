using Microsoft.EntityFrameworkCore;
using SensorMonitoring.Contracts;
using SensorMonitoring.Data.Entities;

namespace SensorMonitoring.Data;

public class SensorDbContext : DbContext
{
    public SensorDbContext(DbContextOptions<SensorDbContext> options)
        : base(options)
    {
    }

    public DbSet<Sensor> Sensors => Set<Sensor>();

    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();

    public DbSet<ConsensusValue> ConsensusValues => Set<ConsensusValue>();

    public DbSet<AlarmEvent> AlarmEvents => Set<AlarmEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sensor>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.DataQuality).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<SensorReading>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AlarmPriority).HasConversion<int>();

            entity.HasOne(e => e.Sensor)
                .WithMany(s => s.Readings)
                .HasForeignKey(e => e.SensorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.SensorId, e.Timestamp });
        });

        modelBuilder.Entity<ConsensusValue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.PeriodStart).IsUnique();
        });

        modelBuilder.Entity<AlarmEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Priority).HasConversion<int>();

            entity.HasOne(e => e.Sensor)
                .WithMany(s => s.AlarmEvents)
                .HasForeignKey(e => e.SensorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.SensorId, e.Timestamp });
        });

        SeedSensors(modelBuilder);
    }

    private static void SeedSensors(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sensor>().HasData(
            new Sensor
            {
                Id = "SENSOR-001",
                TemperatureMin = -10,
                TemperatureMax = 35,
                DataQuality = DataQuality.Good,
                AlarmThreshold1 = 30,
                AlarmThreshold2 = 32,
                AlarmThreshold3 = 34,
                IsActive = true
            },
            new Sensor
            {
                Id = "SENSOR-002",
                TemperatureMin = 0,
                TemperatureMax = 40,
                DataQuality = DataQuality.Good,
                AlarmThreshold1 = 35,
                AlarmThreshold2 = 37,
                AlarmThreshold3 = 39,
                IsActive = true
            },
            new Sensor
            {
                Id = "SENSOR-003",
                TemperatureMin = 5,
                TemperatureMax = 45,
                DataQuality = DataQuality.Good,
                AlarmThreshold1 = 38,
                AlarmThreshold2 = 41,
                AlarmThreshold3 = 44,
                IsActive = true
            },
            new Sensor
            {
                Id = "SENSOR-004",
                TemperatureMin = -5,
                TemperatureMax = 30,
                DataQuality = DataQuality.Good,
                AlarmThreshold1 = 25,
                AlarmThreshold2 = 27,
                AlarmThreshold3 = 29,
                IsActive = true
            },
            new Sensor
            {
                Id = "SENSOR-005",
                TemperatureMin = 10,
                TemperatureMax = 50,
                DataQuality = DataQuality.Good,
                AlarmThreshold1 = 42,
                AlarmThreshold2 = 46,
                AlarmThreshold3 = 49,
                IsActive = true
            },
            new Sensor
            {
                Id = "SENSOR-006",
                TemperatureMin = -15,
                TemperatureMax = 25,
                DataQuality = DataQuality.Good,
                AlarmThreshold1 = 20,
                AlarmThreshold2 = 22,
                AlarmThreshold3 = 24,
                IsActive = false
            },
            new Sensor
            {
                Id = "SENSOR-007",
                TemperatureMin = 15,
                TemperatureMax = 55,
                DataQuality = DataQuality.Good,
                AlarmThreshold1 = 48,
                AlarmThreshold2 = 51,
                AlarmThreshold3 = 54,
                IsActive = false
            });
    }
}
