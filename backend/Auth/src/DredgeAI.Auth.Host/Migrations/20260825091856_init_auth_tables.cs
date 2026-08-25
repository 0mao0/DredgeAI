using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.Migrations
{
    /// <inheritdoc />
    public partial class init_auth_tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tab_openid_dict_applications",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_application_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "应用类型"),
                    f_client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "客户端Id"),
                    f_client_secret = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "客户端密钥"),
                    f_client_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "客户端类型"),
                    f_consent_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "同意类型"),
                    f_display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "显示名称"),
                    f_display_names = table.Column<string>(type: "text", nullable: true, comment: "国际化的显示名称"),
                    f_json_web_key_set = table.Column<string>(type: "text", nullable: true, comment: "密钥"),
                    f_permissions = table.Column<string>(type: "text", nullable: true, comment: "权限集合"),
                    f_post_logout_redirect_uris = table.Column<string>(type: "text", nullable: true, comment: "退出登录后的回调"),
                    f_properties = table.Column<string>(type: "text", nullable: true, comment: "属性"),
                    f_redirect_uris = table.Column<string>(type: "text", nullable: true, comment: "登录后的回调"),
                    f_requirements = table.Column<string>(type: "text", nullable: true, comment: "必须启用的资源"),
                    f_settings = table.Column<string>(type: "text", nullable: true, comment: "设置"),
                    f_front_channel_logout_uri = table.Column<string>(type: "text", nullable: true),
                    f_client_uri = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true, comment: "客户端Uri"),
                    f_logo_uri = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true, comment: "品牌图标"),
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
                    table.PrimaryKey("PK_tab_openid_dict_applications", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_openid_dict_scopes",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_description = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "描述"),
                    f_descriptions = table.Column<string>(type: "text", nullable: true, comment: "描述(国际化)"),
                    f_display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "显示名称"),
                    f_display_names = table.Column<string>(type: "text", nullable: true, comment: "显示名称(国际化)"),
                    f_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, comment: "范围名称"),
                    f_properties = table.Column<string>(type: "text", nullable: true, comment: "属性"),
                    f_resources = table.Column<string>(type: "text", nullable: true, comment: "资源"),
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
                    table.PrimaryKey("PK_tab_openid_dict_scopes", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_openid_dict_authorizations",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_application_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "所属应用Id"),
                    f_creation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "创建时间（UTC）"),
                    f_properties = table.Column<string>(type: "text", nullable: true, comment: "属性"),
                    f_scopes = table.Column<string>(type: "text", nullable: true, comment: "授权范围"),
                    f_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "状态"),
                    f_subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true, comment: "主题"),
                    f_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "类型"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: false, comment: "拓展字段"),
                    f_concurrency_stamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "并发标识")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_openid_dict_authorizations", x => x.f_id);
                    table.ForeignKey(
                        name: "FK_tab_openid_dict_authorizations_tab_openid_dict_applications~",
                        column: x => x.f_application_id,
                        principalTable: "tab_openid_dict_applications",
                        principalColumn: "f_id");
                });

            migrationBuilder.CreateTable(
                name: "tab_openid_dict_tokens",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_application_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "应用Id"),
                    f_authorization_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "授权Id"),
                    f_creation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "创建时间（UTC）"),
                    f_expiration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "过期时间（UTC）"),
                    f_payload = table.Column<string>(type: "text", nullable: true, comment: "票据内容"),
                    f_properties = table.Column<string>(type: "text", nullable: true, comment: "属性"),
                    f_redemption_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "赎回时间"),
                    f_reference_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "关联Id"),
                    f_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "状态"),
                    f_subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true, comment: "主题"),
                    f_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true, comment: "类型"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: false, comment: "拓展字段"),
                    f_concurrency_stamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "并发标识")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_openid_dict_tokens", x => x.f_id);
                    table.ForeignKey(
                        name: "FK_tab_openid_dict_tokens_tab_openid_dict_applications_f_appli~",
                        column: x => x.f_application_id,
                        principalTable: "tab_openid_dict_applications",
                        principalColumn: "f_id");
                    table.ForeignKey(
                        name: "FK_tab_openid_dict_tokens_tab_openid_dict_authorizations_f_aut~",
                        column: x => x.f_authorization_id,
                        principalTable: "tab_openid_dict_authorizations",
                        principalColumn: "f_id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_tab_openid_dict_applications_f_client_id",
                table: "tab_openid_dict_applications",
                column: "f_client_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_openid_dict_authorizations_f_application_id_f_status_f_~",
                table: "tab_openid_dict_authorizations",
                columns: new[] { "f_application_id", "f_status", "f_subject", "f_type" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_openid_dict_scopes_f_name",
                table: "tab_openid_dict_scopes",
                column: "f_name");

            migrationBuilder.CreateIndex(
                name: "IX_tab_openid_dict_tokens_f_application_id_f_status_f_subject_~",
                table: "tab_openid_dict_tokens",
                columns: new[] { "f_application_id", "f_status", "f_subject", "f_type" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_openid_dict_tokens_f_authorization_id",
                table: "tab_openid_dict_tokens",
                column: "f_authorization_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_openid_dict_tokens_f_reference_id",
                table: "tab_openid_dict_tokens",
                column: "f_reference_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tab_openid_dict_scopes");

            migrationBuilder.DropTable(
                name: "tab_openid_dict_tokens");

            migrationBuilder.DropTable(
                name: "tab_openid_dict_authorizations");

            migrationBuilder.DropTable(
                name: "tab_openid_dict_applications");
        }
    }
}
