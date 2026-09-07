using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.Migrations
{
    /// <inheritdoc />
    public partial class Store_full_yarp_config_json : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "f_authorization_policy",
                table: "tab_proxy_route");

            migrationBuilder.DropColumn(
                name: "f_match_hosts_json",
                table: "tab_proxy_route");

            migrationBuilder.DropColumn(
                name: "f_match_methods_json",
                table: "tab_proxy_route");

            migrationBuilder.DropColumn(
                name: "f_match_path",
                table: "tab_proxy_route");

            migrationBuilder.DropColumn(
                name: "f_destinations_json",
                table: "tab_proxy_cluster");

            migrationBuilder.AddColumn<string>(
                name: "f_config_json",
                table: "tab_proxy_route",
                type: "text",
                nullable: false,
                defaultValue: "",
                comment: "完整 YARP RouteConfig JSON（camelCase）");

            migrationBuilder.AddColumn<string>(
                name: "f_config_json",
                table: "tab_proxy_cluster",
                type: "text",
                nullable: false,
                defaultValue: "",
                comment: "完整 YARP ClusterConfig JSON（camelCase）");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "f_config_json",
                table: "tab_proxy_route");

            migrationBuilder.DropColumn(
                name: "f_config_json",
                table: "tab_proxy_cluster");

            migrationBuilder.AddColumn<string>(
                name: "f_authorization_policy",
                table: "tab_proxy_route",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                comment: "授权策略（YARP 内置字面量 anonymous 或 default）");

            migrationBuilder.AddColumn<string>(
                name: "f_match_hosts_json",
                table: "tab_proxy_route",
                type: "text",
                nullable: true,
                comment: "Host 匹配列表 JSON 数组，null 表示不限制");

            migrationBuilder.AddColumn<string>(
                name: "f_match_methods_json",
                table: "tab_proxy_route",
                type: "text",
                nullable: true,
                comment: "HTTP 方法匹配列表 JSON 数组，null 表示不限制");

            migrationBuilder.AddColumn<string>(
                name: "f_match_path",
                table: "tab_proxy_route",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                comment: "路径匹配模式，如 /api/compare/{**catch-all}");

            migrationBuilder.AddColumn<string>(
                name: "f_destinations_json",
                table: "tab_proxy_cluster",
                type: "text",
                nullable: false,
                defaultValue: "",
                comment: "目的地字典 JSON（destinationId → 下游地址）");
        }
    }
}
