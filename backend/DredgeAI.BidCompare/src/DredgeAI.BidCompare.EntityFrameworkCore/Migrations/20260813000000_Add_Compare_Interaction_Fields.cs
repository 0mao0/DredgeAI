using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.BidCompare.Migrations
{
    /// <inheritdoc />
    public partial class Add_Compare_Interaction_Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoCompareOnParseComplete",
                table: "BcCompareTasks",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NameEditedByUser",
                table: "BcCompareTasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PairsJson",
                table: "BcCompareTasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedName",
                table: "BcCompareTasks",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoCompareOnParseComplete",
                table: "BcCompareTasks");

            migrationBuilder.DropColumn(
                name: "NameEditedByUser",
                table: "BcCompareTasks");

            migrationBuilder.DropColumn(
                name: "PairsJson",
                table: "BcCompareTasks");

            migrationBuilder.DropColumn(
                name: "SuggestedName",
                table: "BcCompareTasks");
        }
    }
}
