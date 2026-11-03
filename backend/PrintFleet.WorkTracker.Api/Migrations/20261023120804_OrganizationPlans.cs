using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintFleet.WorkTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "Projects",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medium");

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentPlanId",
                table: "MonthlyWorkSheets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DepartmentMonthlyPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Risks = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Comments = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentMonthlyPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentMonthlyPlans_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyWorkSheets_DepartmentPlanId",
                table: "MonthlyWorkSheets",
                column: "DepartmentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentMonthlyPlans_DepartmentId_Year_Month",
                table: "DepartmentMonthlyPlans",
                columns: new[] { "DepartmentId", "Year", "Month" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MonthlyWorkSheets_DepartmentMonthlyPlans_DepartmentPlanId",
                table: "MonthlyWorkSheets",
                column: "DepartmentPlanId",
                principalTable: "DepartmentMonthlyPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Users_OwnerId",
                table: "Projects",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Link existing sheets only when the employee already has a department. Sheets whose user has no department stay unlinked.
            migrationBuilder.Sql("""
                INSERT INTO "DepartmentMonthlyPlans" ("Id", "DepartmentId", "Year", "Month", "Status", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), u."DepartmentId", s."Year", s."Month", 'Open', NOW(), NOW()
                FROM "MonthlyWorkSheets" s
                JOIN "Users" u ON u."Id" = s."UserId"
                WHERE u."DepartmentId" IS NOT NULL
                GROUP BY u."DepartmentId", s."Year", s."Month";

                UPDATE "MonthlyWorkSheets" AS s
                SET "DepartmentPlanId" = p."Id"
                FROM "Users" AS u, "DepartmentMonthlyPlans" AS p
                WHERE s."UserId" = u."Id"
                  AND u."DepartmentId" IS NOT NULL
                  AND p."DepartmentId" = u."DepartmentId"
                  AND p."Year" = s."Year"
                  AND p."Month" = s."Month";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MonthlyWorkSheets_DepartmentMonthlyPlans_DepartmentPlanId",
                table: "MonthlyWorkSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Users_OwnerId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "DepartmentMonthlyPlans");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyWorkSheets_DepartmentPlanId",
                table: "MonthlyWorkSheets");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DepartmentPlanId",
                table: "MonthlyWorkSheets");
        }
    }
}
