using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.Migrations
{
    /// <inheritdoc />
    public partial class AddGatewayFiled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "f_description",
                table: "tab_proxy_route",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                comment: "路由描述");

            migrationBuilder.AddColumn<string>(
                name: "f_description",
                table: "tab_proxy_cluster",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                comment: "集群描述");

            migrationBuilder.AddColumn<bool>(
                name: "f_is_enabled",
                table: "tab_proxy_cluster",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                comment: "是否启用；禁用的集群及其路由不进 YARP 快照");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "f_description",
                table: "tab_proxy_route");

            migrationBuilder.DropColumn(
                name: "f_description",
                table: "tab_proxy_cluster");

            migrationBuilder.DropColumn(
                name: "f_is_enabled",
                table: "tab_proxy_cluster");
        }
    }
}
