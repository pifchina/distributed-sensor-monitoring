using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SensorMonitoring.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsensusValues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CalculatedValue = table.Column<double>(type: "double precision", nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsensusValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sensors",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TemperatureMin = table.Column<double>(type: "double precision", nullable: false),
                    TemperatureMax = table.Column<double>(type: "double precision", nullable: false),
                    DataQuality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AlarmThreshold1 = table.Column<double>(type: "double precision", nullable: false),
                    AlarmThreshold2 = table.Column<double>(type: "double precision", nullable: false),
                    AlarmThreshold3 = table.Column<double>(type: "double precision", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsBlockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sensors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlarmEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SensorId = table.Column<string>(type: "character varying(64)", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlarmEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlarmEvents_Sensors_SensorId",
                        column: x => x.SensorId,
                        principalTable: "Sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SensorReadings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SensorId = table.Column<string>(type: "character varying(64)", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsConsensus = table.Column<bool>(type: "boolean", nullable: false),
                    AlarmPriority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensorReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SensorReadings_Sensors_SensorId",
                        column: x => x.SensorId,
                        principalTable: "Sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Sensors",
                columns: new[] { "Id", "AlarmThreshold1", "AlarmThreshold2", "AlarmThreshold3", "DataQuality", "IsActive", "IsBlockedUntil", "LastMessageAt", "TemperatureMax", "TemperatureMin" },
                values: new object[,]
                {
                    { "SENSOR-001", 30.0, 32.0, 34.0, "Good", true, null, null, 35.0, -10.0 },
                    { "SENSOR-002", 35.0, 37.0, 39.0, "Good", true, null, null, 40.0, 0.0 },
                    { "SENSOR-003", 38.0, 41.0, 44.0, "Good", true, null, null, 45.0, 5.0 },
                    { "SENSOR-004", 25.0, 27.0, 29.0, "Good", true, null, null, 30.0, -5.0 },
                    { "SENSOR-005", 42.0, 46.0, 49.0, "Good", true, null, null, 50.0, 10.0 },
                    { "SENSOR-006", 20.0, 22.0, 24.0, "Good", false, null, null, 25.0, -15.0 },
                    { "SENSOR-007", 48.0, 51.0, 54.0, "Good", false, null, null, 55.0, 15.0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlarmEvents_SensorId_Timestamp",
                table: "AlarmEvents",
                columns: new[] { "SensorId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsensusValues_Timestamp",
                table: "ConsensusValues",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_SensorId_Timestamp",
                table: "SensorReadings",
                columns: new[] { "SensorId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlarmEvents");

            migrationBuilder.DropTable(
                name: "ConsensusValues");

            migrationBuilder.DropTable(
                name: "SensorReadings");

            migrationBuilder.DropTable(
                name: "Sensors");
        }
    }
}
