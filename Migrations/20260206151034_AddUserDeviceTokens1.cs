using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDeviceTokens1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserDeviceTokens_UserId_ExpoPushToken",
                table: "UserDeviceTokens",
                columns: new[] { "UserId", "ExpoPushToken" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserDeviceTokens_UserId_ExpoPushToken",
                table: "UserDeviceTokens");
        }
    }
}
