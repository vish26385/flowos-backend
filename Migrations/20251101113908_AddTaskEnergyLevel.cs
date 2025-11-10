using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskEnergyLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnergyLevel",
                table: "Tasks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnergyLevel",
                table: "Tasks");
        }
    }
}
