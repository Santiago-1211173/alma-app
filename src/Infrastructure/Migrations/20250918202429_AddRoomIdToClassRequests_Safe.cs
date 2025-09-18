using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlmaApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomIdToClassRequests_Safe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "ClassRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ClassRequests_RoomId_ProposedStartUtc",
                table: "ClassRequests",
                columns: new[] { "RoomId", "ProposedStartUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClassRequests_Rooms_RoomId",
                table: "ClassRequests",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassRequests_Rooms_RoomId",
                table: "ClassRequests");

            migrationBuilder.DropIndex(
                name: "IX_ClassRequests_RoomId_ProposedStartUtc",
                table: "ClassRequests");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "ClassRequests");
        }
    }
}
