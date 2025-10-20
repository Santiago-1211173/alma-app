using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlmaApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientMembershipAndBiometricSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BiometricSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TakenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUid = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    WeightMinKg = table.Column<double>(type: "float", nullable: true),
                    WeightMaxKg = table.Column<double>(type: "float", nullable: true),
                    BodyFatKg = table.Column<double>(type: "float", nullable: true),
                    LeanMassKg = table.Column<double>(type: "float", nullable: true),
                    VisceralFatIndex = table.Column<double>(type: "float", nullable: true),
                    BodyMassIndex = table.Column<double>(type: "float", nullable: true),
                    HeightCm = table.Column<double>(type: "float", nullable: true),
                    Age = table.Column<int>(type: "int", nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: true),
                    Pathologies = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Allergens = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DietPlan = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SleepHours = table.Column<double>(type: "float", nullable: true),
                    ChestCm = table.Column<double>(type: "float", nullable: true),
                    WaistCm = table.Column<double>(type: "float", nullable: true),
                    AbdomenCm = table.Column<double>(type: "float", nullable: true),
                    HipsCm = table.Column<double>(type: "float", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiometricSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BillingPeriod = table.Column<int>(type: "int", nullable: false),
                    Nif = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUid = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientMemberships", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BiometricSnapshots_ClientId_TakenAtUtc",
                table: "BiometricSnapshots",
                columns: new[] { "ClientId", "TakenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientMemberships_ClientId_StartUtc",
                table: "ClientMemberships",
                columns: new[] { "ClientId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientMemberships_ClientId_Status",
                table: "ClientMemberships",
                columns: new[] { "ClientId", "Status" },
                unique: true,
                filter: "[Status] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BiometricSnapshots");

            migrationBuilder.DropTable(
                name: "ClientMemberships");
        }
    }
}
