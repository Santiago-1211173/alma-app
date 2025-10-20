using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlmaApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkshopsActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Activities");

            migrationBuilder.RenameColumn(
                name: "StartUtc",
                table: "Activities",
                newName: "StartLocal");

            migrationBuilder.RenameIndex(
                name: "IX_Activities_RoomId_StartUtc",
                table: "Activities",
                newName: "IX_Activities_RoomId_StartLocal");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Activities",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByUid",
                table: "Activities",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Activities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtLocal",
                table: "Activities",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "InstructorId",
                table: "Activities",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "MaxParticipants",
                table: "Activities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ActivityParticipants",
                columns: table => new
                {
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedAtLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeftAtLocal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityParticipants", x => new { x.ActivityId, x.ClientId });
                    table.ForeignKey(
                        name: "FK_ActivityParticipants_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_InstructorId_StartLocal",
                table: "Activities",
                columns: new[] { "InstructorId", "StartLocal" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityParticipants_ClientId",
                table: "ActivityParticipants",
                column: "ClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityParticipants");

            migrationBuilder.DropIndex(
                name: "IX_Activities_InstructorId_StartLocal",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "CreatedAtLocal",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "InstructorId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "MaxParticipants",
                table: "Activities");

            migrationBuilder.RenameColumn(
                name: "StartLocal",
                table: "Activities",
                newName: "StartUtc");

            migrationBuilder.RenameIndex(
                name: "IX_Activities_RoomId_StartLocal",
                table: "Activities",
                newName: "IX_Activities_RoomId_StartUtc");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Activities",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByUid",
                table: "Activities",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Activities",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");
        }
    }
}
