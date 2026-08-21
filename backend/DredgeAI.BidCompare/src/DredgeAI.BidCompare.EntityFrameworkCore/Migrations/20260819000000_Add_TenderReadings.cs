using System;
using DredgeAI.BidCompare.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.BidCompare.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BidCompareDbContext))]
    [Migration("20260819000000_Add_TenderReadings")]
    public partial class Add_TenderReadings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BcTenderReadingTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    ProgressStage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false),
                    BaselineVersion = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_BcTenderReadingTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BcTenderReadingDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FileExtension = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    OriginStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ParseStatus = table.Column<byte>(type: "smallint", nullable: false),
                    AnGineerDocId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IrStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DocMdStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ParseError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ParseProgress = table.Column<int>(type: "integer", nullable: true),
                    ParseStage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ParseStageMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ParseStartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ParseFinishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PageCount = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_BcTenderReadingDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BcBaselineFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<byte>(type: "smallint", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ValueJson = table.Column<string>(type: "text", nullable: false),
                    RawText = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Extractor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExtractorVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_BcBaselineFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BcSourceMapItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PageIdx = table.Column<int>(type: "integer", nullable: false),
                    BboxJson = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_BcSourceMapItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BcTenderReadingTasks_ProjectCode",
                table: "BcTenderReadingTasks",
                column: "ProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_BcTenderReadingTasks_Status",
                table: "BcTenderReadingTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BcTenderReadingDocuments_TaskId",
                table: "BcTenderReadingDocuments",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_BcBaselineFields_TaskId_Category",
                table: "BcBaselineFields",
                columns: new[] { "TaskId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_BcBaselineFields_TaskId_FieldKey",
                table: "BcBaselineFields",
                columns: new[] { "TaskId", "FieldKey" });

            migrationBuilder.CreateIndex(
                name: "IX_BcSourceMapItems_FieldId",
                table: "BcSourceMapItems",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_BcSourceMapItems_FieldId_PageIdx",
                table: "BcSourceMapItems",
                columns: new[] { "FieldId", "PageIdx" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BcSourceMapItems");
            migrationBuilder.DropTable(name: "BcBaselineFields");
            migrationBuilder.DropTable(name: "BcTenderReadingDocuments");
            migrationBuilder.DropTable(name: "BcTenderReadingTasks");
        }
    }
}
