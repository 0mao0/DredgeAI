using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.BidCompare.Migrations
{
    /// <inheritdoc />
    public partial class Add_CompareTask_ClauseDraftsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClauseDraftsJson",
                table: "BcCompareTasks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClauseDraftsJson",
                table: "BcCompareTasks");
        }
    }
}
