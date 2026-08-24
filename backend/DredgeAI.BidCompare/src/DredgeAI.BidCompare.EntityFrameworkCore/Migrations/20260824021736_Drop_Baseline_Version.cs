using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.BidCompare.Migrations
{
    /// <inheritdoc />
    public partial class Drop_Baseline_Version : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaselineVersion",
                table: "BcTenderReadingTasks");

            migrationBuilder.DropColumn(
                name: "TenderReadingBaselineVersion",
                table: "BcCompareTasks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BaselineVersion",
                table: "BcTenderReadingTasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenderReadingBaselineVersion",
                table: "BcCompareTasks",
                type: "integer",
                nullable: true);
        }
    }
}
