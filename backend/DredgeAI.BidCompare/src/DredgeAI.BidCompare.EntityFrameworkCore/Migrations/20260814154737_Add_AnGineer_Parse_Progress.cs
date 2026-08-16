using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.BidCompare.Migrations
{
    /// <inheritdoc />
    public partial class Add_AnGineer_Parse_Progress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ParseFinishedAt",
                table: "BcCompareDocuments",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParseProgress",
                table: "BcCompareDocuments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParseStage",
                table: "BcCompareDocuments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParseStageMessage",
                table: "BcCompareDocuments",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ParseStartedAt",
                table: "BcCompareDocuments",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParseFinishedAt",
                table: "BcCompareDocuments");

            migrationBuilder.DropColumn(
                name: "ParseProgress",
                table: "BcCompareDocuments");

            migrationBuilder.DropColumn(
                name: "ParseStage",
                table: "BcCompareDocuments");

            migrationBuilder.DropColumn(
                name: "ParseStageMessage",
                table: "BcCompareDocuments");

            migrationBuilder.DropColumn(
                name: "ParseStartedAt",
                table: "BcCompareDocuments");
        }
    }
}
