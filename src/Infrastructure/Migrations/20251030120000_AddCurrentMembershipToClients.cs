using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlmaApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentMembershipToClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentMembershipId",
                table: "Clients",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CurrentMembershipId",
                table: "Clients",
                column: "CurrentMembershipId",
                unique: true,
                filter: "[CurrentMembershipId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_CurrentMembershipId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CurrentMembershipId",
                table: "Clients");
        }
    }
}
