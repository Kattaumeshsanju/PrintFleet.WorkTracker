using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintFleet.WorkTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class PlanReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanReminderDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepartmentName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MessageType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Retryable = table.Column<bool>(type: "boolean", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureDetail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanReminderDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanReminderRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AcceptedCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    FailureDetail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanReminderRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanReminderSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    MissingPlanEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DepartmentSummaryEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Hour = table.Column<int>(type: "integer", nullable: false),
                    Minute = table.Column<int>(type: "integer", nullable: false),
                    Weekdays = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanReminderSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanReminderDeliveries_IdempotencyKey",
                table: "PlanReminderDeliveries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanReminderDeliveries_ScheduledDate_Status",
                table: "PlanReminderDeliveries",
                columns: new[] { "ScheduledDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanReminderRuns_ScheduledDate",
                table: "PlanReminderRuns",
                column: "ScheduledDate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanReminderDeliveries");

            migrationBuilder.DropTable(
                name: "PlanReminderRuns");

            migrationBuilder.DropTable(
                name: "PlanReminderSettings");
        }
    }
}
