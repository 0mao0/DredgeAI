using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.Migrations
{
    /// <inheritdoc />
    public partial class init_base_tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tab_audit_log_excel_files",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    f_creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    f_creator_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "创建人ID")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_audit_log_excel_files", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_audit_logs",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_application_name = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true, comment: "应用名"),
                    f_user_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "用户ID"),
                    f_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "用户名"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_tenant_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "租户名"),
                    f_impersonator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    f_impersonator_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    f_impersonator_tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    f_impersonator_tenant_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    f_execution_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "发生异常时间"),
                    f_execution_duration = table.Column<int>(type: "integer", nullable: false, comment: "执行时长（毫秒）"),
                    f_client_ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "客户端IP地址"),
                    f_client_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "客户端名称"),
                    f_client_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "客户端Id"),
                    f_correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "相关Id"),
                    f_browser_info = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "浏览器信息"),
                    f_http_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true, comment: "请求方法"),
                    f_url = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "请求URL"),
                    f_exceptions = table.Column<string>(type: "text", nullable: true, comment: "异常信息"),
                    f_comments = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "注释"),
                    f_http_status_code = table.Column<int>(type: "integer", nullable: true, comment: "请求状态码"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: false, comment: "拓展字段"),
                    f_concurrency_stamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "并发标识")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_audit_logs", x => x.f_id);
                },
                comment: "审计日志表");

            migrationBuilder.CreateTable(
                name: "tab_block_file",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_system_file_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "文件Id"),
                    f_file_md5 = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true, comment: "文件Md5值"),
                    f_status = table.Column<int>(type: "integer", nullable: false, comment: "分块文件状态"),
                    f_merge_result = table.Column<string>(type: "text", nullable: true, comment: "合并结果"),
                    f_creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    f_last_modification_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "修改时间"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: false, comment: "拓展字段"),
                    f_concurrency_stamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "并发标识")
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
                    f_identifier = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, comment: "每个文件的唯一标示"),
                    f_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "文件名"),
                    f_total_chunks = table.Column<int>(type: "integer", nullable: false, comment: "文件被分成块的总数"),
                    f_chunk_size = table.Column<long>(type: "bigint", nullable: false, comment: "分块大小，根据 totalSize 和这个值你就可以计算出总共的块数。注意最后一块的大小可能会比这个要大"),
                    f_chunk_number = table.Column<int>(type: "integer", nullable: false, comment: "当前块的次序，第一个块是 1，注意不是从 0 开始的"),
                    f_current_chunk_size = table.Column<long>(type: "bigint", nullable: false, comment: "当前块的大小，实际大小"),
                    f_total_size = table.Column<long>(type: "bigint", nullable: false, comment: "文件总大小"),
                    f_path = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "文件路径"),
                    f_creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: false, comment: "拓展字段"),
                    f_concurrency_stamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "并发标识")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_block_file_temp", x => x.f_id);
                },
                comment: "分块文件缓存");

            migrationBuilder.CreateTable(
                name: "tab_dict_data",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_type_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属字典类型ID，外键关联 DictType"),
                    f_parent_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "父数据值ID，null 表示根数据值，同类型内构建层级"),
                    f_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "数据值编码（业务唯一标识），格式：{TypeCode}_{8位随机大写字母数字}，如 DEFAULT_GENDER_A7K3X9M2"),
                    f_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "数据值，同类型同层级内唯一"),
                    f_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "数据值显示名称"),
                    f_sort = table.Column<int>(type: "integer", nullable: false, comment: "排序号，同层级内按此升序排列"),
                    f_is_enabled = table.Column<bool>(type: "boolean", nullable: false, comment: "是否启用（true:启用 false:禁用），禁用后不在下拉选项中展示"),
                    f_remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "备注"),
                    f_is_static = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "是否静态数据，静态数据不允许修改和删除"),
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
                    table.PrimaryKey("PK_tab_dict_data", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_dict_type",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "字典类型名称，同层级内唯一"),
                    f_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "字典类型编码（业务唯一标识），格式：{模块前缀}_{6位随机大写字母数字}，如 DEFAULT_A7K3X9"),
                    f_full_code = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "层级路径编码（点分4位数字自增），如 0001 / 0001.0002，表示树形层级关系"),
                    f_parent_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "父类型ID，null 表示根节点"),
                    f_module_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "绑定功能模块编号，用于 Code 编码前缀，null 时默认使用 DEFAULT"),
                    f_sort = table.Column<int>(type: "integer", nullable: false, comment: "排序号，同级内按此升序排列"),
                    f_remark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "备注"),
                    f_is_static = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "是否静态数据，静态数据不允许修改和删除"),
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
                    table.PrimaryKey("PK_tab_dict_type", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_feature_groups",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "特性组名"),
                    f_display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "显示名"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: true, comment: "拓展字段")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_feature_groups", x => x.f_id);
                },
                comment: "特性组定义记录表");

            migrationBuilder.CreateTable(
                name: "tab_feature_value",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "特性名"),
                    f_value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "特性值"),
                    f_provider_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "特性提供器名"),
                    f_provider_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "特性所属")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_feature_value", x => x.f_id);
                },
                comment: "特性管理表");

            migrationBuilder.CreateTable(
                name: "tab_features",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_group_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "特性组名"),
                    f_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "特性名"),
                    f_parent_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "父特性名"),
                    f_display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "显示名"),
                    f_description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "描述"),
                    f_default_value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "默认值"),
                    f_is_visible_to_clients = table.Column<bool>(type: "boolean", nullable: false, comment: "对客户端可见"),
                    f_is_available_to_host = table.Column<bool>(type: "boolean", nullable: false, comment: "对主机可用"),
                    f_allowed_providers = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "允许的提供器"),
                    f_value_type = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true, comment: "值类型"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: true, comment: "拓展字段")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_features", x => x.f_id);
                },
                comment: "特性定义记录表");

            migrationBuilder.CreateTable(
                name: "tab_identity_claim_types",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "名称"),
                    f_required = table.Column<bool>(type: "boolean", nullable: false, comment: "是否必须"),
                    f_is_static = table.Column<bool>(type: "boolean", nullable: false, comment: "是否静态"),
                    f_regex = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "正则"),
                    f_regex_description = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "正则描述"),
                    f_description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "描述"),
                    f_value_type = table.Column<int>(type: "integer", nullable: false, comment: "值类型"),
                    f_creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: false, comment: "拓展字段"),
                    f_concurrency_stamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "并发标识")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_claim_types", x => x.f_id);
                },
                comment: "声明类型表");

            migrationBuilder.CreateTable(
                name: "tab_identity_link_users",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_source_user_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "源用户ID"),
                    f_source_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "源租户ID"),
                    f_target_user_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "目标用户ID"),
                    f_target_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "目标租户ID")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_link_users", x => x.f_id);
                },
                comment: "用户连接表");

            migrationBuilder.CreateTable(
                name: "tab_identity_organization_units",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_parent_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "父级ID"),
                    f_code = table.Column<string>(type: "character varying(95)", maxLength: 95, nullable: false, comment: "编号"),
                    f_display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "显示名"),
                    f_entity_version = table.Column<int>(type: "integer", nullable: false, comment: "实体版本"),
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
                    table.PrimaryKey("PK_tab_identity_organization_units", x => x.f_id);
                    table.ForeignKey(
                        name: "FK_tab_identity_organization_units_tab_identity_organization_u~",
                        column: x => x.f_parent_id,
                        principalTable: "tab_identity_organization_units",
                        principalColumn: "f_id");
                },
                comment: "组织表");

            migrationBuilder.CreateTable(
                name: "tab_identity_roles",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "名称"),
                    f_normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "标准名称"),
                    f_is_default = table.Column<bool>(type: "boolean", nullable: false, comment: "是否默认"),
                    f_is_static = table.Column<bool>(type: "boolean", nullable: false, comment: "是否静态"),
                    f_is_public = table.Column<bool>(type: "boolean", nullable: false, comment: "是否公共"),
                    f_entity_version = table.Column<int>(type: "integer", nullable: false, comment: "实体版本"),
                    f_creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: false, comment: "拓展字段"),
                    f_concurrency_stamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "并发标识")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_roles", x => x.f_id);
                },
                comment: "角色表");

            migrationBuilder.CreateTable(
                name: "tab_identity_security_logs",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_application_name = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true, comment: "应用名"),
                    f_identity = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true, comment: "身份"),
                    f_action = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true, comment: "行为"),
                    f_user_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "用户ID"),
                    f_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "用户名"),
                    f_tenant_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "租户名"),
                    f_client_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "客户端Ip"),
                    f_correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "关联ID"),
                    f_client_ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "客户端Ip地址"),
                    f_browser_info = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "浏览器信息"),
                    f_creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: false, comment: "拓展字段"),
                    f_concurrency_stamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "并发标识")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_security_logs", x => x.f_id);
                },
                comment: "安全日志表");

            migrationBuilder.CreateTable(
                name: "tab_identity_sessions",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_session_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    f_device = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    f_device_info = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    f_client_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    f_ip_addresses = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    f_signed_in = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    f_last_accessed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    f_extra_properties = table.Column<string>(type: "text", nullable: true, comment: "拓展字段")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_sessions", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_identity_user_delegations",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_source_user_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "源用户ID"),
                    f_target_user_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "目标用户ID"),
                    f_start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "开始时间"),
                    f_end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "结束时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_user_delegations", x => x.f_id);
                },
                comment: "用户委托表");

            migrationBuilder.CreateTable(
                name: "tab_identity_user_extension",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_user_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "关联的 IdentityUser ID"),
                    f_expire_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "账号过期时间，null 表示永不过期"),
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
                    table.PrimaryKey("PK_tab_identity_user_extension", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_identity_users",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "用户名"),
                    f_normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "标准用户名"),
                    f_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "姓名"),
                    f_surname = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "姓"),
                    f_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "邮件"),
                    f_normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "标准邮件"),
                    f_email_confirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "邮件确认"),
                    f_password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "密码"),
                    f_security_stamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "安全戳"),
                    f_is_external = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "是否外部人员"),
                    f_phone_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true, comment: "手机号"),
                    f_phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "手机号确认"),
                    f_is_active = table.Column<bool>(type: "boolean", nullable: false, comment: "是否激活"),
                    f_two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "两步验证是否开启"),
                    f_lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "锁定结束时间"),
                    f_lockout_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "是否开启锁定"),
                    f_access_failed_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "认证失败次数"),
                    f_should_change_password_on_next_login = table.Column<bool>(type: "boolean", nullable: false, comment: "下一次登录是否修改密码"),
                    f_entity_version = table.Column<int>(type: "integer", nullable: false, comment: "实体版本"),
                    f_last_password_change_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "最后一次修改密码的时间"),
                    f_last_sign_in_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    f_leaved = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_tab_identity_users", x => x.f_id);
                },
                comment: "用户表");

            migrationBuilder.CreateTable(
                name: "tab_menu_info",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_parent_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "上级菜单ID"),
                    f_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "菜单类型（Directory=目录 Menu=菜单 Button=按钮）"),
                    f_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "路由名称"),
                    f_title = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "显示标题"),
                    f_component_path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "前端组件路径"),
                    f_route_path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "路由路径"),
                    f_redirect_path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "重定向地址"),
                    f_icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "图标"),
                    f_icon_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "图标类型（Ali/Element/FontAwesome）"),
                    f_route_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "路由类型（Default/IframeUrl/OpenWindow）"),
                    f_permission_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "权限编码"),
                    f_sort_id = table.Column<long>(type: "bigint", nullable: false, comment: "排序号，同级内按此升序排列"),
                    f_is_enabled = table.Column<bool>(type: "boolean", nullable: false, comment: "是否启用"),
                    f_is_cache = table.Column<bool>(type: "boolean", nullable: false, comment: "是否缓存"),
                    f_is_fixed = table.Column<bool>(type: "boolean", nullable: false, comment: "是否固定"),
                    f_is_hidden = table.Column<bool>(type: "boolean", nullable: false, comment: "是否隐藏"),
                    f_is_static = table.Column<bool>(type: "boolean", nullable: false, comment: "系统菜单保护标识"),
                    f_remark = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "备注"),
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
                    table.PrimaryKey("PK_tab_menu_info", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_permission_grants",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "权限名"),
                    f_provider_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "权限提供器名"),
                    f_provider_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "权限所属")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_permission_grants", x => x.f_id);
                },
                comment: "权限表");

            migrationBuilder.CreateTable(
                name: "tab_permission_groups",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "权限组名"),
                    f_display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "显示名"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: true, comment: "拓展字段")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_permission_groups", x => x.f_id);
                },
                comment: "权限组定义记录表");

            migrationBuilder.CreateTable(
                name: "tab_permissions",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_group_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "权限组名"),
                    f_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "权限名"),
                    f_resource_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "资源名称"),
                    f_management_permission_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "管理权限名"),
                    f_parent_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "父权限名"),
                    f_display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "显示名"),
                    f_is_enabled = table.Column<bool>(type: "boolean", nullable: false, comment: "是否启用"),
                    f_multi_tenancy_side = table.Column<byte>(type: "smallint", nullable: false, comment: "多租户端"),
                    f_providers = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "提供器"),
                    f_state_checkers = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "状态检查器"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: true, comment: "拓展字段")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_permissions", x => x.f_id);
                },
                comment: "权限定义记录表");

            migrationBuilder.CreateTable(
                name: "tab_resource_permission_grants",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    f_provider_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    f_provider_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    f_resource_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    f_resource_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_resource_permission_grants", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_setting_definitions",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "设置名"),
                    f_display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "显示名"),
                    f_description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "描述"),
                    f_default_value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true, comment: "默认值"),
                    f_is_visible_to_clients = table.Column<bool>(type: "boolean", nullable: false, comment: "是否对客户端可见"),
                    f_providers = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true, comment: "提供器"),
                    f_is_inherited = table.Column<bool>(type: "boolean", nullable: false, comment: "是否继承"),
                    f_is_encrypted = table.Column<bool>(type: "boolean", nullable: false, comment: "是否加密"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: true, comment: "拓展字段")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_setting_definitions", x => x.f_id);
                },
                comment: "设置定义记录表");

            migrationBuilder.CreateTable(
                name: "tab_settings",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "设置名"),
                    f_value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false, comment: "设置值"),
                    f_provider_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "提供器名"),
                    f_provider_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "设置所属")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_settings", x => x.f_id);
                },
                comment: "设置表");

            migrationBuilder.CreateTable(
                name: "tab_system_file",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_source_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "源ID"),
                    f_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "文件名"),
                    f_file_extension_name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true, comment: "文件拓展名"),
                    f_physical_file_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "物理文件名"),
                    f_path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "文件路径"),
                    f_mime_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, comment: "文件类型"),
                    f_type = table.Column<byte>(type: "smallint", nullable: true, comment: "所属类型"),
                    f_sort_id = table.Column<long>(type: "bigint", nullable: false, comment: "排序字段"),
                    f_is_block_upload = table.Column<bool>(type: "boolean", nullable: false, comment: "是否分块上传"),
                    f_is_block_merged = table.Column<bool>(type: "boolean", nullable: false, comment: "分块是否已合并"),
                    f_block_merge_time = table.Column<long>(type: "bigint", nullable: false, comment: "分块文件合并耗时（秒）"),
                    f_file_size_kb = table.Column<long>(type: "bigint", nullable: false, comment: "文件大小（KB）"),
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
                    table.PrimaryKey("PK_tab_system_file", x => x.f_id);
                },
                comment: "系统文件表");

            migrationBuilder.CreateTable(
                name: "tab_tenants",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "租户名"),
                    f_normalized_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "标准租户名"),
                    f_entity_version = table.Column<int>(type: "integer", nullable: false, comment: "实体版本"),
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
                    table.PrimaryKey("PK_tab_tenants", x => x.f_id);
                },
                comment: "租户表");

            migrationBuilder.CreateTable(
                name: "tab_audit_log_actions",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_audit_log_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属审计日志ID"),
                    f_service_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "调用服务的完整类名"),
                    f_method_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "方法名"),
                    f_parameters = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, comment: "请求参数"),
                    f_execution_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "请求时间"),
                    f_execution_duration = table.Column<int>(type: "integer", nullable: false, comment: "请求时长（毫秒）"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: true, comment: "拓展字段")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_audit_log_actions", x => x.f_id);
                    table.ForeignKey(
                        name: "FK_tab_audit_log_actions_tab_audit_logs_f_audit_log_id",
                        column: x => x.f_audit_log_id,
                        principalTable: "tab_audit_logs",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "审计日志请求表");

            migrationBuilder.CreateTable(
                name: "tab_entity_changes",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_audit_log_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属审计日志ID"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_change_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "改变时间"),
                    f_change_type = table.Column<byte>(type: "smallint", nullable: false, comment: "改变类型"),
                    f_entity_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "实体租户ID"),
                    f_entity_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "请求时长（毫秒）"),
                    f_entity_type_full_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, comment: "实体类完整名"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: true, comment: "拓展字段")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_entity_changes", x => x.f_id);
                    table.ForeignKey(
                        name: "FK_tab_entity_changes_tab_audit_logs_f_audit_log_id",
                        column: x => x.f_audit_log_id,
                        principalTable: "tab_audit_logs",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "实体变化表");

            migrationBuilder.CreateTable(
                name: "tab_identity_organization_unit_roles",
                columns: table => new
                {
                    f_role_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "角色ID"),
                    f_organization_unit_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "组织ID"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    f_creator_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "创建人ID")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_organization_unit_roles", x => new { x.f_organization_unit_id, x.f_role_id });
                    table.ForeignKey(
                        name: "FK_tab_identity_organization_unit_roles_tab_identity_organizat~",
                        column: x => x.f_organization_unit_id,
                        principalTable: "tab_identity_organization_units",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tab_identity_organization_unit_roles_tab_identity_roles_f_r~",
                        column: x => x.f_role_id,
                        principalTable: "tab_identity_roles",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "组织角色表");

            migrationBuilder.CreateTable(
                name: "tab_identity_role_claims",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_role_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "角色ID"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_claim_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "声明类型"),
                    f_claim_value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true, comment: "声明值")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_role_claims", x => x.f_id);
                    table.ForeignKey(
                        name: "FK_tab_identity_role_claims_tab_identity_roles_f_role_id",
                        column: x => x.f_role_id,
                        principalTable: "tab_identity_roles",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "用户角色声明表");

            migrationBuilder.CreateTable(
                name: "tab_identity_user_claims",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_user_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "用户ID"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_claim_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "声明类型"),
                    f_claim_value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true, comment: "声明值")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_user_claims", x => x.f_id);
                    table.ForeignKey(
                        name: "FK_tab_identity_user_claims_tab_identity_users_f_user_id",
                        column: x => x.f_user_id,
                        principalTable: "tab_identity_users",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "用户声明表");

            migrationBuilder.CreateTable(
                name: "tab_identity_user_logins",
                columns: table => new
                {
                    f_user_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "用户ID"),
                    f_login_provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "登录提供器"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_provider_key = table.Column<string>(type: "character varying(196)", maxLength: 196, nullable: false, comment: "提供器唯一KEY"),
                    f_provider_display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "提供器名称")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_user_logins", x => new { x.f_user_id, x.f_login_provider });
                    table.ForeignKey(
                        name: "FK_tab_identity_user_logins_tab_identity_users_f_user_id",
                        column: x => x.f_user_id,
                        principalTable: "tab_identity_users",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tab_identity_user_organization_units",
                columns: table => new
                {
                    f_user_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "用户ID"),
                    f_organization_unit_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "组织ID"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    f_creator_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "创建人ID")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_user_organization_units", x => new { x.f_organization_unit_id, x.f_user_id });
                    table.ForeignKey(
                        name: "FK_tab_identity_user_organization_units_tab_identity_organizat~",
                        column: x => x.f_organization_unit_id,
                        principalTable: "tab_identity_organization_units",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tab_identity_user_organization_units_tab_identity_users_f_u~",
                        column: x => x.f_user_id,
                        principalTable: "tab_identity_users",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "用户组织表");

            migrationBuilder.CreateTable(
                name: "tab_identity_user_passkeys",
                columns: table => new
                {
                    f_credential_id = table.Column<byte[]>(type: "bytea", maxLength: 1024, nullable: false),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    f_data = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_user_passkeys", x => x.f_credential_id);
                    table.ForeignKey(
                        name: "FK_tab_identity_user_passkeys_tab_identity_users_f_user_id",
                        column: x => x.f_user_id,
                        principalTable: "tab_identity_users",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tab_identity_user_password_histories",
                columns: table => new
                {
                    f_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    f_password = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_user_password_histories", x => new { x.f_user_id, x.f_password });
                    table.ForeignKey(
                        name: "FK_tab_identity_user_password_histories_tab_identity_users_f_u~",
                        column: x => x.f_user_id,
                        principalTable: "tab_identity_users",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tab_identity_user_roles",
                columns: table => new
                {
                    f_user_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "用户ID"),
                    f_role_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "角色ID"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_user_roles", x => new { x.f_user_id, x.f_role_id });
                    table.ForeignKey(
                        name: "FK_tab_identity_user_roles_tab_identity_roles_f_role_id",
                        column: x => x.f_role_id,
                        principalTable: "tab_identity_roles",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tab_identity_user_roles_tab_identity_users_f_user_id",
                        column: x => x.f_user_id,
                        principalTable: "tab_identity_users",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "用户角色表");

            migrationBuilder.CreateTable(
                name: "tab_identity_user_tokens",
                columns: table => new
                {
                    f_user_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "用户ID"),
                    f_login_provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "登录提供器"),
                    f_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "名称"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_value = table.Column<string>(type: "text", nullable: true, comment: "值")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_identity_user_tokens", x => new { x.f_user_id, x.f_login_provider, x.f_name });
                    table.ForeignKey(
                        name: "FK_tab_identity_user_tokens_tab_identity_users_f_user_id",
                        column: x => x.f_user_id,
                        principalTable: "tab_identity_users",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "用户Token表");

            migrationBuilder.CreateTable(
                name: "tab_tenant_connection_strings",
                columns: table => new
                {
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属租户"),
                    f_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "名称"),
                    f_value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false, comment: "连接字符串")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_tenant_connection_strings", x => new { x.f_tenant_id, x.f_name });
                    table.ForeignKey(
                        name: "FK_tab_tenant_connection_strings_tab_tenants_f_tenant_id",
                        column: x => x.f_tenant_id,
                        principalTable: "tab_tenants",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "租户连接字符串");

            migrationBuilder.CreateTable(
                name: "tab_entity_property_changes",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_tenant_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "租户ID"),
                    f_entity_change_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属实体改变ID"),
                    f_new_value = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "新值"),
                    f_original_value = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "原始值"),
                    f_property_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "属性名"),
                    f_property_type_full_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, comment: "属性类型")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_entity_property_changes", x => x.f_id);
                    table.ForeignKey(
                        name: "FK_tab_entity_property_changes_tab_entity_changes_f_entity_cha~",
                        column: x => x.f_entity_change_id,
                        principalTable: "tab_entity_changes",
                        principalColumn: "f_id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "实体属性变化表");

            migrationBuilder.CreateIndex(
                name: "IX_tab_audit_log_actions_f_audit_log_id",
                table: "tab_audit_log_actions",
                column: "f_audit_log_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_audit_log_actions_f_tenant_id_f_service_name_f_method_n~",
                table: "tab_audit_log_actions",
                columns: new[] { "f_tenant_id", "f_service_name", "f_method_name", "f_execution_time" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_audit_logs_f_execution_time",
                table: "tab_audit_logs",
                column: "f_execution_time");

            migrationBuilder.CreateIndex(
                name: "IX_tab_audit_logs_f_http_status_code",
                table: "tab_audit_logs",
                column: "f_http_status_code");

            migrationBuilder.CreateIndex(
                name: "IX_tab_audit_logs_f_tenant_id",
                table: "tab_audit_logs",
                column: "f_tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_audit_logs_f_url",
                table: "tab_audit_logs",
                column: "f_url");

            migrationBuilder.CreateIndex(
                name: "IX_tab_audit_logs_f_user_id",
                table: "tab_audit_logs",
                column: "f_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_block_file_f_system_file_id",
                table: "tab_block_file",
                column: "f_system_file_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_block_file_temp_f_identifier_f_chunk_number",
                table: "tab_block_file_temp",
                columns: new[] { "f_identifier", "f_chunk_number" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_dict_data_f_code",
                table: "tab_dict_data",
                column: "f_code",
                unique: true,
                filter: "f_is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_tab_dict_data_f_parent_id",
                table: "tab_dict_data",
                column: "f_parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_dict_data_f_type_id",
                table: "tab_dict_data",
                column: "f_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_dict_data_f_type_id_f_parent_id_f_value",
                table: "tab_dict_data",
                columns: new[] { "f_type_id", "f_parent_id", "f_value" },
                unique: true,
                filter: "f_is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_tab_dict_type_f_code",
                table: "tab_dict_type",
                column: "f_code",
                unique: true,
                filter: "f_is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_tab_dict_type_f_full_code",
                table: "tab_dict_type",
                column: "f_full_code",
                unique: true,
                filter: "f_is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_tab_dict_type_f_name",
                table: "tab_dict_type",
                column: "f_name",
                unique: true,
                filter: "f_is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_tab_dict_type_f_parent_id",
                table: "tab_dict_type",
                column: "f_parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_entity_changes_f_audit_log_id",
                table: "tab_entity_changes",
                column: "f_audit_log_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_entity_changes_f_tenant_id_f_entity_type_full_name_f_en~",
                table: "tab_entity_changes",
                columns: new[] { "f_tenant_id", "f_entity_type_full_name", "f_entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_entity_property_changes_f_entity_change_id",
                table: "tab_entity_property_changes",
                column: "f_entity_change_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_feature_groups_f_name",
                table: "tab_feature_groups",
                column: "f_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tab_feature_value_f_name",
                table: "tab_feature_value",
                column: "f_name");

            migrationBuilder.CreateIndex(
                name: "IX_tab_feature_value_f_name_f_provider_name_f_provider_key",
                table: "tab_feature_value",
                columns: new[] { "f_name", "f_provider_name", "f_provider_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tab_feature_value_f_provider_key",
                table: "tab_feature_value",
                column: "f_provider_key");

            migrationBuilder.CreateIndex(
                name: "IX_tab_feature_value_f_provider_name",
                table: "tab_feature_value",
                column: "f_provider_name");

            migrationBuilder.CreateIndex(
                name: "IX_tab_features_f_group_name",
                table: "tab_features",
                column: "f_group_name");

            migrationBuilder.CreateIndex(
                name: "IX_tab_features_f_name",
                table: "tab_features",
                column: "f_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_link_users_f_source_user_id_f_source_tenant_id~",
                table: "tab_identity_link_users",
                columns: new[] { "f_source_user_id", "f_source_tenant_id", "f_target_user_id", "f_target_tenant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_organization_unit_roles_f_role_id_f_organizati~",
                table: "tab_identity_organization_unit_roles",
                columns: new[] { "f_role_id", "f_organization_unit_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_organization_units_f_code",
                table: "tab_identity_organization_units",
                column: "f_code");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_organization_units_f_display_name",
                table: "tab_identity_organization_units",
                column: "f_display_name");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_organization_units_f_is_deleted",
                table: "tab_identity_organization_units",
                column: "f_is_deleted");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_organization_units_f_parent_id",
                table: "tab_identity_organization_units",
                column: "f_parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_organization_units_f_tenant_id",
                table: "tab_identity_organization_units",
                column: "f_tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_role_claims_f_role_id",
                table: "tab_identity_role_claims",
                column: "f_role_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_roles_f_name",
                table: "tab_identity_roles",
                column: "f_name");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_roles_f_normalized_name",
                table: "tab_identity_roles",
                column: "f_normalized_name");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_roles_f_tenant_id",
                table: "tab_identity_roles",
                column: "f_tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_security_logs_f_action",
                table: "tab_identity_security_logs",
                column: "f_action");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_security_logs_f_application_name",
                table: "tab_identity_security_logs",
                column: "f_application_name");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_security_logs_f_identity",
                table: "tab_identity_security_logs",
                column: "f_identity");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_security_logs_f_tenant_id",
                table: "tab_identity_security_logs",
                column: "f_tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_security_logs_f_user_id",
                table: "tab_identity_security_logs",
                column: "f_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_sessions_f_device",
                table: "tab_identity_sessions",
                column: "f_device");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_sessions_f_session_id",
                table: "tab_identity_sessions",
                column: "f_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_sessions_f_tenant_id_f_user_id",
                table: "tab_identity_sessions",
                columns: new[] { "f_tenant_id", "f_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_user_claims_f_user_id",
                table: "tab_identity_user_claims",
                column: "f_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_user_extension_f_user_id",
                table: "tab_identity_user_extension",
                column: "f_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_user_logins_f_login_provider_f_provider_key",
                table: "tab_identity_user_logins",
                columns: new[] { "f_login_provider", "f_provider_key" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_user_organization_units_f_user_id_f_organizati~",
                table: "tab_identity_user_organization_units",
                columns: new[] { "f_user_id", "f_organization_unit_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_user_passkeys_f_user_id",
                table: "tab_identity_user_passkeys",
                column: "f_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_user_roles_f_role_id_f_user_id",
                table: "tab_identity_user_roles",
                columns: new[] { "f_role_id", "f_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_users_f_email",
                table: "tab_identity_users",
                column: "f_email");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_users_f_normalized_email",
                table: "tab_identity_users",
                column: "f_normalized_email");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_users_f_normalized_user_name",
                table: "tab_identity_users",
                column: "f_normalized_user_name");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_users_f_phone_number",
                table: "tab_identity_users",
                column: "f_phone_number");

            migrationBuilder.CreateIndex(
                name: "IX_tab_identity_users_f_user_name",
                table: "tab_identity_users",
                column: "f_user_name");

            migrationBuilder.CreateIndex(
                name: "IX_tab_menu_info_f_name_f_parent_id",
                table: "tab_menu_info",
                columns: new[] { "f_name", "f_parent_id" },
                unique: true,
                filter: "f_is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_tab_menu_info_f_parent_id",
                table: "tab_menu_info",
                column: "f_parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_menu_info_f_type",
                table: "tab_menu_info",
                column: "f_type");

            migrationBuilder.CreateIndex(
                name: "IX_tab_permission_grants_f_tenant_id_f_provider_name_f_provide~",
                table: "tab_permission_grants",
                columns: new[] { "f_tenant_id", "f_provider_name", "f_provider_key", "f_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tab_permission_groups_f_name",
                table: "tab_permission_groups",
                column: "f_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tab_permissions_f_group_name",
                table: "tab_permissions",
                column: "f_group_name");

            migrationBuilder.CreateIndex(
                name: "IX_tab_permissions_f_name",
                table: "tab_permissions",
                column: "f_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tab_resource_permission_grants_f_tenant_id_f_name_f_resourc~",
                table: "tab_resource_permission_grants",
                columns: new[] { "f_tenant_id", "f_name", "f_resource_name", "f_resource_key", "f_provider_name", "f_provider_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tab_setting_definitions_f_name",
                table: "tab_setting_definitions",
                column: "f_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tab_settings_f_name_f_provider_name_f_provider_key",
                table: "tab_settings",
                columns: new[] { "f_name", "f_provider_name", "f_provider_key" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_tab_tenants_f_name",
                table: "tab_tenants",
                column: "f_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tab_audit_log_actions");

            migrationBuilder.DropTable(
                name: "tab_audit_log_excel_files");

            migrationBuilder.DropTable(
                name: "tab_block_file");

            migrationBuilder.DropTable(
                name: "tab_block_file_temp");

            migrationBuilder.DropTable(
                name: "tab_dict_data");

            migrationBuilder.DropTable(
                name: "tab_dict_type");

            migrationBuilder.DropTable(
                name: "tab_entity_property_changes");

            migrationBuilder.DropTable(
                name: "tab_feature_groups");

            migrationBuilder.DropTable(
                name: "tab_feature_value");

            migrationBuilder.DropTable(
                name: "tab_features");

            migrationBuilder.DropTable(
                name: "tab_identity_claim_types");

            migrationBuilder.DropTable(
                name: "tab_identity_link_users");

            migrationBuilder.DropTable(
                name: "tab_identity_organization_unit_roles");

            migrationBuilder.DropTable(
                name: "tab_identity_role_claims");

            migrationBuilder.DropTable(
                name: "tab_identity_security_logs");

            migrationBuilder.DropTable(
                name: "tab_identity_sessions");

            migrationBuilder.DropTable(
                name: "tab_identity_user_claims");

            migrationBuilder.DropTable(
                name: "tab_identity_user_delegations");

            migrationBuilder.DropTable(
                name: "tab_identity_user_extension");

            migrationBuilder.DropTable(
                name: "tab_identity_user_logins");

            migrationBuilder.DropTable(
                name: "tab_identity_user_organization_units");

            migrationBuilder.DropTable(
                name: "tab_identity_user_passkeys");

            migrationBuilder.DropTable(
                name: "tab_identity_user_password_histories");

            migrationBuilder.DropTable(
                name: "tab_identity_user_roles");

            migrationBuilder.DropTable(
                name: "tab_identity_user_tokens");

            migrationBuilder.DropTable(
                name: "tab_menu_info");

            migrationBuilder.DropTable(
                name: "tab_permission_grants");

            migrationBuilder.DropTable(
                name: "tab_permission_groups");

            migrationBuilder.DropTable(
                name: "tab_permissions");

            migrationBuilder.DropTable(
                name: "tab_resource_permission_grants");

            migrationBuilder.DropTable(
                name: "tab_setting_definitions");

            migrationBuilder.DropTable(
                name: "tab_settings");

            migrationBuilder.DropTable(
                name: "tab_system_file");

            migrationBuilder.DropTable(
                name: "tab_tenant_connection_strings");

            migrationBuilder.DropTable(
                name: "tab_entity_changes");

            migrationBuilder.DropTable(
                name: "tab_identity_organization_units");

            migrationBuilder.DropTable(
                name: "tab_identity_roles");

            migrationBuilder.DropTable(
                name: "tab_identity_users");

            migrationBuilder.DropTable(
                name: "tab_tenants");

            migrationBuilder.DropTable(
                name: "tab_audit_logs");
        }
    }
}
