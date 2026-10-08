using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketFlow.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class EventLocationOrganizerFkAndSectionUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sections_EventId",
                table: "sections");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "events");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "events",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "events",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_sections_EventId_Name",
                table: "sections",
                columns: new[] { "EventId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_events_OrganizerId",
                table: "events",
                column: "OrganizerId");

            migrationBuilder.CreateIndex(
                name: "IX_events_Status_Date",
                table: "events",
                columns: new[] { "Status", "Date" });

            migrationBuilder.AddForeignKey(
                name: "FK_events_users_OrganizerId",
                table: "events",
                column: "OrganizerId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_events_users_OrganizerId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_sections_EventId_Name",
                table: "sections");

            migrationBuilder.DropIndex(
                name: "IX_events_OrganizerId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_Status_Date",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "events");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "events");

            migrationBuilder.AddColumn<Guid>(
                name: "VenueId",
                table: "events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_sections_EventId",
                table: "sections",
                column: "EventId");
        }
    }
}
