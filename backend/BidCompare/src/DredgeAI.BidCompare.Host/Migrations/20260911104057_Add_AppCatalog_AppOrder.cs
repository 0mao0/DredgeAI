using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.BidCompare.Migrations
{
    /// <inheritdoc />
    public partial class Add_AppCatalog_AppOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tab_app_catalog",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_parent_app_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "所属主应用 id；null = 主应用"),
                    f_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "应用名称"),
                    f_category = table.Column<byte>(type: "smallint", nullable: false, comment: "应用分类"),
                    f_icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "antd 图标名"),
                    f_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "版本号（如 v2.1.0）"),
                    f_status = table.Column<byte>(type: "smallint", nullable: false, comment: "状态：主应用 Online/Offline，子应用 Published/Unpublished"),
                    f_route = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "路由：主应用为 admin 侧路由，子应用为 user-web 路由（可空）"),
                    f_scope = table.Column<byte>(type: "smallint", nullable: false, comment: "授权范围：Public（默认）/Private"),
                    f_manager = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true, comment: "负责人（仅主应用）"),
                    f_user_count = table.Column<int>(type: "integer", nullable: true, comment: "使用人数（仅主应用，展示指标）"),
                    f_api_calls = table.Column<int>(type: "integer", nullable: true, comment: "API 调用量（仅主应用，展示指标）"),
                    f_user_route = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "user-web 侧边栏路由（仅无子应用的主应用）"),
                    f_description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "子应用描述（仅子应用）"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: false, comment: "拓展字段"),
                    f_concurrency_stamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "并发标识"),
                    f_creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    f_creator_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "创建人ID"),
                    f_last_modification_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "修改时间"),
                    f_last_modifier_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "修改者ID"),
                    f_is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "是否删除 0.否 1.是"),
                    f_deleter_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "删除者ID"),
                    f_deletion_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "删除时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_app_catalog", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_app_order",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_level = table.Column<byte>(type: "smallint", nullable: false, comment: "排序级别：Global 全局 / User 用户"),
                    f_user_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "用户 id；全局行为 Guid.Empty"),
                    f_target_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "排序目标：全局=应用目录条目 id，用户=应用路由"),
                    f_sort_order = table.Column<int>(type: "integer", nullable: false, comment: "顺序，小在前"),
                    f_creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    f_creator_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "创建人ID"),
                    f_last_modification_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "修改时间"),
                    f_last_modifier_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "修改者ID"),
                    f_is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "是否删除 0.否 1.是"),
                    f_deleter_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "删除者ID"),
                    f_deletion_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "删除时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_app_order", x => x.f_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tab_app_catalog_f_parent_app_id",
                table: "tab_app_catalog",
                column: "f_parent_app_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_app_order_f_level_f_user_id_f_target_id",
                table: "tab_app_order",
                columns: new[] { "f_level", "f_user_id", "f_target_id" },
                unique: true,
                filter: "f_is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tab_app_catalog");

            migrationBuilder.DropTable(
                name: "tab_app_order");
        }
    }
}
