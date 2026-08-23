using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.BidCompare.Migrations
{
    /// <inheritdoc />
    public partial class Add_MeetingBot_Entities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BcAttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Team = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
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
                    table.PrimaryKey("PK_BcAttendanceRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BcMeetingRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PreInfoJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SpeechDraftId = table.Column<Guid>(type: "uuid", nullable: true),
                    TranscriptFile = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TranscriptText = table.Column<string>(type: "text", nullable: true),
                    ReportFile = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ReportError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_BcMeetingRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BcQaRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    IntentType = table.Column<byte>(type: "smallint", nullable: false),
                    SourcesJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
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
                    table.PrimaryKey("PK_BcQaRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BcSpeechDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
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
                    table.PrimaryKey("PK_BcSpeechDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BcWorkerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmployeeNo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Team = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FaceStatus = table.Column<byte>(type: "smallint", nullable: false),
                    FacePhotosJson = table.Column<string>(type: "text", nullable: false),
                    FaceEnrolledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_BcWorkerProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BcAttendanceRecords_MeetingRecordId",
                table: "BcAttendanceRecords",
                column: "MeetingRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_BcAttendanceRecords_MeetingRecordId_WorkerId",
                table: "BcAttendanceRecords",
                columns: new[] { "MeetingRecordId", "WorkerId" });

            migrationBuilder.CreateIndex(
                name: "IX_BcMeetingRecords_Date",
                table: "BcMeetingRecords",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_BcMeetingRecords_Status",
                table: "BcMeetingRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BcQaRecords_MeetingRecordId",
                table: "BcQaRecords",
                column: "MeetingRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_BcSpeechDrafts_MeetingRecordId",
                table: "BcSpeechDrafts",
                column: "MeetingRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BcWorkerProfiles_EmployeeNo",
                table: "BcWorkerProfiles",
                column: "EmployeeNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BcAttendanceRecords");

            migrationBuilder.DropTable(
                name: "BcMeetingRecords");

            migrationBuilder.DropTable(
                name: "BcQaRecords");

            migrationBuilder.DropTable(
                name: "BcSpeechDrafts");

            migrationBuilder.DropTable(
                name: "BcWorkerProfiles");
        }
    }
}
