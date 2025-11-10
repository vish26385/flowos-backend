using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddModelUsedToDailyPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModelUsed",
                table: "DailyPlans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredTone",
                table: "AspNetUsers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModelUsed",
                table: "DailyPlans");

            migrationBuilder.DropColumn(
                name: "PreferredTone",
                table: "AspNetUsers");
        }
    }
}
