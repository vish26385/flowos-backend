using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanJsonRaw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PlanJson",
                table: "DailyPlans",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "PlanJsonRaw",
                table: "DailyPlans",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlanJsonRaw",
                table: "DailyPlans");

            migrationBuilder.AlterColumn<string>(
                name: "PlanJson",
                table: "DailyPlans",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
