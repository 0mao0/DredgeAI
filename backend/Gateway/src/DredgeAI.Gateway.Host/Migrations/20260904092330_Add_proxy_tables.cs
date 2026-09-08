using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.Migrations
{
    /// <inheritdoc />
    public partial class Add_proxy_tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tab_proxy_cluster",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_cluster_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "YARP 集群 ID，全局唯一"),
                    f_destinations_json = table.Column<string>(type: "text", nullable: false, comment: "目的地字典 JSON（destinationId → 下游地址）"),
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
                    table.PrimaryKey("PK_tab_proxy_cluster", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_proxy_route",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_route_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "YARP 路由 ID，全局唯一"),
                    f_cluster_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "引用的集群 ID"),
                    f_order = table.Column<int>(type: "integer", nullable: false, comment: "路由匹配优先级（值越小越优先）"),
                    f_match_path = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "路径匹配模式，如 /api/compare/{**catch-all}"),
                    f_match_hosts_json = table.Column<string>(type: "text", nullable: true, comment: "Host 匹配列表 JSON 数组，null 表示不限制"),
                    f_match_methods_json = table.Column<string>(type: "text", nullable: true, comment: "HTTP 方法匹配列表 JSON 数组，null 表示不限制"),
                    f_authorization_policy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "授权策略（YARP 内置字面量 anonymous 或 default）"),
                    f_is_enabled = table.Column<bool>(type: "boolean", nullable: false, comment: "是否启用；禁用的路由不进 YARP 快照"),
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
                    table.PrimaryKey("PK_tab_proxy_route", x => x.f_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tab_proxy_cluster_f_cluster_id",
                table: "tab_proxy_cluster",
                column: "f_cluster_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tab_proxy_route_f_route_id",
                table: "tab_proxy_route",
                column: "f_route_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tab_proxy_cluster");

            migrationBuilder.DropTable(
                name: "tab_proxy_route");
        }
    }
}
