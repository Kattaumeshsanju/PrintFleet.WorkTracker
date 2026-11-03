using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintFleet.WorkTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class ResearchCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkCategoryId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InitiativeId",
                table: "MonthlyTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkCategoryId",
                table: "MonthlyTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NameKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkCategories_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Initiatives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetStart = table.Column<DateOnly>(type: "date", nullable: true),
                    TargetEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResearchObjective = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Hypothesis = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResearchStage = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SuccessCriteria = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ExperimentPlan = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Conclusion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Initiatives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Initiatives_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Initiatives_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Initiatives_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Initiatives_WorkCategories_WorkCategoryId",
                        column: x => x.WorkCategoryId,
                        principalTable: "WorkCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InitiativeCollaborators",
                columns: table => new
                {
                    InitiativeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InitiativeCollaborators", x => new { x.InitiativeId, x.UserId });
                    table.ForeignKey(
                        name: "FK_InitiativeCollaborators_Initiatives_InitiativeId",
                        column: x => x.InitiativeId,
                        principalTable: "Initiatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InitiativeCollaborators_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InitiativeMilestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiativeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CompletedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InitiativeMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InitiativeMilestones_Initiatives_InitiativeId",
                        column: x => x.InitiativeId,
                        principalTable: "Initiatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InitiativeMilestones_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResearchRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiativeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecordedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Objective = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Method = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Findings = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EvidenceLinks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NextSteps = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResearchRecords_Initiatives_InitiativeId",
                        column: x => x.InitiativeId,
                        principalTable: "Initiatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_WorkCategoryId",
                table: "Projects",
                column: "WorkCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyTasks_InitiativeId",
                table: "MonthlyTasks",
                column: "InitiativeId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyTasks_WorkCategoryId",
                table: "MonthlyTasks",
                column: "WorkCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InitiativeCollaborators_UserId",
                table: "InitiativeCollaborators",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InitiativeMilestones_InitiativeId",
                table: "InitiativeMilestones",
                column: "InitiativeId");

            migrationBuilder.CreateIndex(
                name: "IX_InitiativeMilestones_OwnerId",
                table: "InitiativeMilestones",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Initiatives_DepartmentId_Status",
                table: "Initiatives",
                columns: new[] { "DepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Initiatives_Number",
                table: "Initiatives",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Initiatives_OwnerId",
                table: "Initiatives",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Initiatives_ProjectId",
                table: "Initiatives",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Initiatives_WorkCategoryId",
                table: "Initiatives",
                column: "WorkCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ResearchRecords_InitiativeId_RecordedOn",
                table: "ResearchRecords",
                columns: new[] { "InitiativeId", "RecordedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkCategories_DepartmentId_NameKey",
                table: "WorkCategories",
                columns: new[] { "DepartmentId", "NameKey" },
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.AddForeignKey(
                name: "FK_MonthlyTasks_Initiatives_InitiativeId",
                table: "MonthlyTasks",
                column: "InitiativeId",
                principalTable: "Initiatives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MonthlyTasks_WorkCategories_WorkCategoryId",
                table: "MonthlyTasks",
                column: "WorkCategoryId",
                principalTable: "WorkCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_WorkCategories_WorkCategoryId",
                table: "Projects",
                column: "WorkCategoryId",
                principalTable: "WorkCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MonthlyTasks_Initiatives_InitiativeId",
                table: "MonthlyTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_MonthlyTasks_WorkCategories_WorkCategoryId",
                table: "MonthlyTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_WorkCategories_WorkCategoryId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "InitiativeCollaborators");

            migrationBuilder.DropTable(
                name: "InitiativeMilestones");

            migrationBuilder.DropTable(
                name: "ResearchRecords");

            migrationBuilder.DropTable(
                name: "Initiatives");

            migrationBuilder.DropTable(
                name: "WorkCategories");

            migrationBuilder.DropIndex(
                name: "IX_Projects_WorkCategoryId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyTasks_InitiativeId",
                table: "MonthlyTasks");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyTasks_WorkCategoryId",
                table: "MonthlyTasks");

            migrationBuilder.DropColumn(
                name: "WorkCategoryId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "InitiativeId",
                table: "MonthlyTasks");

            migrationBuilder.DropColumn(
                name: "WorkCategoryId",
                table: "MonthlyTasks");
        }
    }
}
