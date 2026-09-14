using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.Migrations
{
    /// <inheritdoc />
    public partial class update_permission_group_name_allow_null : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tab_block_file");

            migrationBuilder.DropTable(
                name: "tab_block_file_temp");

            migrationBuilder.DropTable(
                name: "tab_system_file");

            migrationBuilder.AlterColumn<string>(
                name: "f_group_name",
                table: "tab_permissions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                comment: "权限组名",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldComment: "权限组名");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "f_group_name",
                table: "tab_permissions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                comment: "权限组名",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "权限组名");

            migrationBuilder.CreateTable(
                name: "tab_block_file",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_concurrency_stamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "并发标识"),
                    f_creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: false, comment: "拓展字段"),
                    f_file_md5 = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true, comment: "文件Md5值"),
                    f_last_modification_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "修改时间"),
                    f_merge_result = table.Column<string>(type: "text", nullable: true, comment: "合并结果"),
                    f_status = table.Column<int>(type: "integer", nullable: false, comment: "分块文件状态"),
                    f_system_file_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "文件Id")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_block_file", x => x.f_id);
                },
                comment: "分块文件表");

            migrationBuilder.CreateTable(
                name: "tab_block_file_temp",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_chunk_number = table.Column<int>(type: "integer", nullable: false, comment: "当前块的次序，第一个块是 1，注意不是从 0 开始的"),
                    f_chunk_size = table.Column<long>(type: "bigint", nullable: false, comment: "分块大小，根据 totalSize 和这个值你就可以计算出总共的块数。注意最后一块的大小可能会比这个要大"),
                    f_concurrency_stamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "并发标识"),
                    f_creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    f_current_chunk_size = table.Column<long>(type: "bigint", nullable: false, comment: "当前块的大小，实际大小"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: false, comment: "拓展字段"),
                    f_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "文件名"),
                    f_identifier = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, comment: "每个文件的唯一标示"),
                    f_path = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "文件路径"),
                    f_total_chunks = table.Column<int>(type: "integer", nullable: false, comment: "文件被分成块的总数"),
                    f_total_size = table.Column<long>(type: "bigint", nullable: false, comment: "文件总大小")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_block_file_temp", x => x.f_id);
                },
                comment: "分块文件缓存");

            migrationBuilder.CreateTable(
                name: "tab_system_file",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_block_merge_time = table.Column<long>(type: "bigint", nullable: false, comment: "分块文件合并耗时（秒）"),
                    f_concurrency_stamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "并发标识"),
                    f_creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    f_creator_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "创建人ID"),
                    f_deleter_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "删除者ID"),
                    f_deletion_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "删除时间"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: false, comment: "拓展字段"),
                    f_file_extension_name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true, comment: "文件拓展名"),
                    f_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "文件名"),
                    f_file_size_kb = table.Column<long>(type: "bigint", nullable: false, comment: "文件大小（KB）"),
                    f_is_block_merged = table.Column<bool>(type: "boolean", nullable: false, comment: "分块是否已合并"),
                    f_is_block_upload = table.Column<bool>(type: "boolean", nullable: false, comment: "是否分块上传"),
                    f_is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "是否删除 0.否 1.是"),
                    f_last_modification_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "修改时间"),
                    f_last_modifier_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "修改者ID"),
                    f_mime_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "文件类型"),
                    f_path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "文件路径"),
                    f_physical_file_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "物理文件名"),
                    f_sort_id = table.Column<long>(type: "bigint", nullable: false, comment: "排序字段"),
                    f_source_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "源ID"),
                    f_type = table.Column<byte>(type: "smallint", nullable: true, comment: "所属类型")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_system_file", x => x.f_id);
                },
                comment: "系统文件表");

            migrationBuilder.CreateIndex(
                name: "IX_tab_block_file_f_system_file_id",
                table: "tab_block_file",
                column: "f_system_file_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_block_file_temp_f_identifier_f_chunk_number",
                table: "tab_block_file_temp",
                columns: new[] { "f_identifier", "f_chunk_number" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_system_file_f_is_deleted",
                table: "tab_system_file",
                column: "f_is_deleted");

            migrationBuilder.CreateIndex(
                name: "IX_tab_system_file_f_path",
                table: "tab_system_file",
                column: "f_path");

            migrationBuilder.CreateIndex(
                name: "IX_tab_system_file_f_source_id",
                table: "tab_system_file",
                column: "f_source_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_system_file_f_type",
                table: "tab_system_file",
                column: "f_type");
        }
    }
}
