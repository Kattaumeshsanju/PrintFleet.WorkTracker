using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintFleet.WorkTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class WorkRecordHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "MonthlyWorkSheets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ActualEndDate",
                table: "MonthlyTasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ActualStartDate",
                table: "MonthlyTasks",
                type: "date",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "MonthlyWorkSheets" AS sheet
                SET "DepartmentId" = person."DepartmentId"
                FROM "Users" AS person
                WHERE sheet."UserId" = person."Id"
                  AND sheet."DepartmentId" IS NULL
                  AND person."DepartmentId" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyWorkSheets_DepartmentId",
                table: "MonthlyWorkSheets",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyWorkSheets_DepartmentId_Year_Month",
                table: "MonthlyWorkSheets",
                columns: new[] { "DepartmentId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyTasks_Status_PlannedEndDate",
                table: "MonthlyTasks",
                columns: new[] { "Status", "PlannedEndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyWorkEntries_UserId_WorkDate_StartTime",
                table: "DailyWorkEntries",
                columns: new[] { "UserId", "WorkDate", "StartTime" });

            migrationBuilder.AddForeignKey(
                name: "FK_MonthlyWorkSheets_Departments_DepartmentId",
                table: "MonthlyWorkSheets",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MonthlyWorkSheets_Departments_DepartmentId",
                table: "MonthlyWorkSheets");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyWorkSheets_DepartmentId",
                table: "MonthlyWorkSheets");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyWorkSheets_DepartmentId_Year_Month",
                table: "MonthlyWorkSheets");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyTasks_Status_PlannedEndDate",
                table: "MonthlyTasks");

            migrationBuilder.DropIndex(
                name: "IX_DailyWorkEntries_UserId_WorkDate_StartTime",
                table: "DailyWorkEntries");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "MonthlyWorkSheets");

            migrationBuilder.DropColumn(
                name: "ActualEndDate",
                table: "MonthlyTasks");

            migrationBuilder.DropColumn(
                name: "ActualStartDate",
                table: "MonthlyTasks");
        }
    }
}
