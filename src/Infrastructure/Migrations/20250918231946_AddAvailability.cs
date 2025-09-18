using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlmaApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoomClosures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomClosures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffAvailabilityRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTimeUtc = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTimeUtc = table.Column<TimeSpan>(type: "time", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffAvailabilityRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffTimeOffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffTimeOffs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomClosures_RoomId_FromUtc",
                table: "RoomClosures",
                columns: new[] { "RoomId", "FromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffAvailabilityRules_StaffId_DayOfWeek",
                table: "StaffAvailabilityRules",
                columns: new[] { "StaffId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffTimeOffs_StaffId_FromUtc",
                table: "StaffTimeOffs",
                columns: new[] { "StaffId", "FromUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomClosures");

            migrationBuilder.DropTable(
                name: "StaffAvailabilityRules");

            migrationBuilder.DropTable(
                name: "StaffTimeOffs");
        }
    }
}
