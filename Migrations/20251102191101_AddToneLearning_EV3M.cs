using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FlowOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddToneLearning_EV3M : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentTone",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastToneChangeDate",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToneConfidence",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ToneHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmotionalScore = table.Column<int>(type: "integer", nullable: false),
                    PerformanceScore = table.Column<int>(type: "integer", nullable: false),
                    SuggestedTone = table.Column<string>(type: "text", nullable: true),
                    AppliedTone = table.Column<string>(type: "text", nullable: true),
                    ConfidenceDelta = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToneHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToneHistories_UserId_Date",
                table: "ToneHistories",
                columns: new[] { "UserId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToneHistories");

            migrationBuilder.DropColumn(
                name: "CurrentTone",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastToneChangeDate",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ToneConfidence",
                table: "AspNetUsers");
        }
    }
}
