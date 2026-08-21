using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.BidCompare.Migrations
{
    /// <inheritdoc />
    public partial class Add_CompareTask_TenderReadingBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TenderReadingBaselineVersion",
                table: "BcCompareTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenderReadingTaskId",
                table: "BcCompareTasks",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenderReadingBaselineVersion",
                table: "BcCompareTasks");

            migrationBuilder.DropColumn(
                name: "TenderReadingTaskId",
                table: "BcCompareTasks");
        }
    }
}
