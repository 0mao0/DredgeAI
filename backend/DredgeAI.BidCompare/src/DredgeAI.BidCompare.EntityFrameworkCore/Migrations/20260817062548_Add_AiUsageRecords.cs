using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.BidCompare.Migrations
{
    /// <inheritdoc />
    public partial class Add_AiUsageRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BcAiUsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Business = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UsedConfig = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UsedModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    TotalTokens = table.Column<int>(type: "integer", nullable: true),
                    FinishReason = table.Column<string>(type: "text", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LatencySeconds = table.Column<double>(type: "double precision", nullable: true),
                    CircuitBreakerState = table.Column<string>(type: "text", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorType = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    TextPreview = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BcAiUsageRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BcAiUsageRecords_Business",
                table: "BcAiUsageRecords",
                column: "Business");

            migrationBuilder.CreateIndex(
                name: "IX_BcAiUsageRecords_CreationTime",
                table: "BcAiUsageRecords",
                column: "CreationTime");

            migrationBuilder.CreateIndex(
                name: "IX_BcAiUsageRecords_Success_CreationTime",
                table: "BcAiUsageRecords",
                columns: new[] { "Success", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_BcAiUsageRecords_UsedConfig",
                table: "BcAiUsageRecords",
                column: "UsedConfig");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BcAiUsageRecords");
        }
    }
}
