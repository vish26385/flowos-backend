using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class _20251102_1430_AddDailyPlanFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyPlans_UserId",
                table: "DailyPlans");

            migrationBuilder.RenameColumn(
                name: "PlanJson",
                table: "DailyPlans",
                newName: "PlanJsonClean");

            migrationBuilder.CreateIndex(
                name: "IX_DailyPlans_UserId_Date",
                table: "DailyPlans",
                columns: new[] { "UserId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyPlans_UserId_Date",
                table: "DailyPlans");

            migrationBuilder.RenameColumn(
                name: "PlanJsonClean",
                table: "DailyPlans",
                newName: "PlanJson");

            migrationBuilder.CreateIndex(
                name: "IX_DailyPlans_UserId",
                table: "DailyPlans",
                column: "UserId");
        }
    }
}
