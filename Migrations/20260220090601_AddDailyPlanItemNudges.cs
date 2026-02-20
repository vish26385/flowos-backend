using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyPlanItemNudges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "Date",
                table: "DailyPlans",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndNudgeAtUtc",
                table: "DailyPlanItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndNudgeSentAtUtc",
                table: "DailyPlanItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastNudgeError",
                table: "DailyPlanItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NudgeSentAtUtc",
                table: "DailyPlanItems",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndNudgeAtUtc",
                table: "DailyPlanItems");

            migrationBuilder.DropColumn(
                name: "EndNudgeSentAtUtc",
                table: "DailyPlanItems");

            migrationBuilder.DropColumn(
                name: "LastNudgeError",
                table: "DailyPlanItems");

            migrationBuilder.DropColumn(
                name: "NudgeSentAtUtc",
                table: "DailyPlanItems");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "DailyPlans",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");
        }
    }
}
