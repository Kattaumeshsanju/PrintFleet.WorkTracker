using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintFleet.WorkTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReminderConsistency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Transport",
                table: "PlanReminderDeliveries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RecipientId",
                table: "EmailDeliveries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "EmailDeliveries",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "TeamNotice");

            migrationBuilder.AddColumn<string>(
                name: "RecipientEmail",
                table: "EmailDeliveries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "EmailDeliveries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "EmailDeliveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WorkDate",
                table: "EmailDeliveries",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DailyReminderEnabled",
                table: "Departments",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveries_Kind_UserId_WorkDate",
                table: "EmailDeliveries",
                columns: new[] { "Kind", "UserId", "WorkDate" },
                unique: true,
                filter: "\"Kind\" = 'DailyReminder'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailDeliveries_Kind_UserId_WorkDate",
                table: "EmailDeliveries");

            migrationBuilder.DropColumn(
                name: "Transport",
                table: "PlanReminderDeliveries");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "EmailDeliveries");

            migrationBuilder.DropColumn(
                name: "RecipientEmail",
                table: "EmailDeliveries");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "EmailDeliveries");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "EmailDeliveries");

            migrationBuilder.DropColumn(
                name: "WorkDate",
                table: "EmailDeliveries");

            migrationBuilder.DropColumn(
                name: "DailyReminderEnabled",
                table: "Departments");

            migrationBuilder.AlterColumn<Guid>(
                name: "RecipientId",
                table: "EmailDeliveries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
