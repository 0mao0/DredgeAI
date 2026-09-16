using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.Migrations
{
    /// <inheritdoc />
    public partial class AddRateLimitPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tab_rate_limit_policy",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "显示名，全局唯一"),
                    f_scope = table.Column<int>(type: "integer", nullable: false, comment: "作用域：0=全局，1=路由级"),
                    f_route_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "目标路由 ID（Scope=Route 时必填）"),
                    f_algorithm = table.Column<int>(type: "integer", nullable: false, comment: "限流算法：0=固定窗口，1=滑动窗口，2=令牌桶"),
                    f_permit_limit = table.Column<int>(type: "integer", nullable: true, comment: "窗口内允许的请求数（Fixed/Sliding 必填）"),
                    f_window_seconds = table.Column<int>(type: "integer", nullable: true, comment: "窗口时长秒数（Fixed/Sliding 必填）"),
                    f_segments_per_window = table.Column<int>(type: "integer", nullable: true, comment: "滑动窗口分段数（Sliding 必填）"),
                    f_token_limit = table.Column<int>(type: "integer", nullable: true, comment: "令牌桶容量（TokenBucket 必填）"),
                    f_tokens_per_period = table.Column<int>(type: "integer", nullable: true, comment: "每周期补充令牌数（TokenBucket 必填）"),
                    f_replenishment_period_seconds = table.Column<int>(type: "integer", nullable: true, comment: "令牌补充周期秒数（TokenBucket 必填）"),
                    f_queue_limit = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "排队上限（>=0，默认 0 不排队）"),
                    f_is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "是否启用；禁用的策略不参与限流解析"),
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
                    table.PrimaryKey("PK_tab_rate_limit_policy", x => x.f_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tab_rate_limit_policy_f_name",
                table: "tab_rate_limit_policy",
                column: "f_name",
                unique: true,
                filter: "f_is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_tab_rate_limit_policy_f_scope_f_route_id",
                table: "tab_rate_limit_policy",
                columns: new[] { "f_scope", "f_route_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tab_rate_limit_policy");
        }
    }
}
