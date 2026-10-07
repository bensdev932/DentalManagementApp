using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicManagementApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Patients_CreatedAtUtc",
                table: "Patients",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Status",
                table: "Patients",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Date",
                table: "Expenses",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Status_AppointmentDateTime",
                table: "Appointments",
                columns: new[] { "Status", "AppointmentDateTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patients_CreatedAtUtc",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_Status",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_Date",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_Status_AppointmentDateTime",
                table: "Appointments");
        }
    }
}

