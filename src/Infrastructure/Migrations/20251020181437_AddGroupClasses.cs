using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlmaApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GroupClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstructorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxParticipants = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupClasses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupClassParticipants",
                columns: table => new
                {
                    GroupClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedAtLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeftAtLocal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupClassParticipants", x => new { x.GroupClassId, x.ClientId });
                    table.ForeignKey(
                        name: "FK_GroupClassParticipants_GroupClasses_GroupClassId",
                        column: x => x.GroupClassId,
                        principalTable: "GroupClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupClasses_InstructorId_StartLocal",
                table: "GroupClasses",
                columns: new[] { "InstructorId", "StartLocal" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupClasses_RoomId_StartLocal",
                table: "GroupClasses",
                columns: new[] { "RoomId", "StartLocal" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupClassParticipants_ClientId",
                table: "GroupClassParticipants",
                column: "ClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupClassParticipants");

            migrationBuilder.DropTable(
                name: "GroupClasses");
        }
    }
}
