using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintFleet.WorkTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class DayMark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DayType",
                table: "DailySubmissions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DayType",
                table: "DailySubmissions");
        }
    }
}
