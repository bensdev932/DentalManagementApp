using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicManagementApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddClientLogEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DeviceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeviceModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AppVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Exception = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientLogEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientLogEntries_CorrelationId",
                table: "ClientLogEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientLogEntries_DeviceId_TimestampUtc",
                table: "ClientLogEntries",
                columns: new[] { "DeviceId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientLogEntries_ReceivedAtUtc",
                table: "ClientLogEntries",
                column: "ReceivedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientLogEntries");
        }
    }
}

