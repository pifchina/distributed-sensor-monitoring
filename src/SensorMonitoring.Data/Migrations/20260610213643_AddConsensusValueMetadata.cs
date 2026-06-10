using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SensorMonitoring.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConsensusValueMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SampleCount",
                table: "ConsensusValues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SensorCount",
                table: "ConsensusValues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ConsensusValues_PeriodStart",
                table: "ConsensusValues",
                column: "PeriodStart",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConsensusValues_PeriodStart",
                table: "ConsensusValues");

            migrationBuilder.DropColumn(
                name: "SampleCount",
                table: "ConsensusValues");

            migrationBuilder.DropColumn(
                name: "SensorCount",
                table: "ConsensusValues");
        }
    }
}
