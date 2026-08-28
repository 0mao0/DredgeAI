using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DredgeAI.BidCompare.Migrations
{
    /// <inheritdoc />
    public partial class Add_init_tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tab_ai_usage_record",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_business = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "业务场景标识"),
                    f_used_config = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "使用的模型配置名"),
                    f_used_model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "实际调用模型名"),
                    f_input_tokens = table.Column<int>(type: "integer", nullable: true, comment: "输入 token 数"),
                    f_output_tokens = table.Column<int>(type: "integer", nullable: true, comment: "输出 token 数"),
                    f_total_tokens = table.Column<int>(type: "integer", nullable: true, comment: "总 token 数"),
                    f_finish_reason = table.Column<string>(type: "text", nullable: true, comment: "模型结束原因"),
                    f_attempts = table.Column<int>(type: "integer", nullable: false, comment: "调用尝试次数（含重试）"),
                    f_latency_seconds = table.Column<double>(type: "double precision", nullable: true, comment: "调用耗时（秒）"),
                    f_circuit_breaker_state = table.Column<string>(type: "text", nullable: true, comment: "熔断器状态"),
                    f_success = table.Column<bool>(type: "boolean", nullable: false, comment: "是否成功"),
                    f_error_type = table.Column<string>(type: "text", nullable: true, comment: "错误类型"),
                    f_error_message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true, comment: "错误信息"),
                    f_text_preview = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "输入文本预览"),
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
                    table.PrimaryKey("PK_tab_ai_usage_record", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_attendance_record",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_meeting_record_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属会议记录 ID"),
                    f_worker_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "识别到的工人 ID，未识别为 null"),
                    f_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "姓名（未识别为空串）"),
                    f_team = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "班组"),
                    f_status = table.Column<byte>(type: "smallint", nullable: false, comment: "出勤状态（Present/Absent/Late/Unrecognized）"),
                    f_confidence = table.Column<double>(type: "double precision", nullable: false, comment: "人脸识别置信度"),
                    f_bbox = table.Column<string>(type: "text", nullable: false, comment: "人脸框 [x1,y1,x2,y2] JSON，未识别人脸去重用，无坐标为 \"[]\""),
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
                    table.PrimaryKey("PK_tab_attendance_record", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_background_jobs",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_application_name = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false, comment: "应用名称"),
                    f_job_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "任务名"),
                    f_job_args = table.Column<string>(type: "character varying(1048576)", maxLength: 1048576, nullable: false, comment: "任务参数"),
                    f_try_count = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0, comment: "尝试次数"),
                    f_creation_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "创建时间"),
                    f_next_try_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "下一次尝试时间"),
                    f_last_try_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "最后一次尝试时间"),
                    f_is_abandoned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "是否不再运行"),
                    f_priority = table.Column<byte>(type: "smallint", nullable: false, defaultValue: (byte)15, comment: "权重"),
                    f_extra_properties = table.Column<string>(type: "text", nullable: false, comment: "拓展字段"),
                    f_concurrency_stamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "并发标识")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tab_background_jobs", x => x.f_id);
                },
                comment: "后台任务记录表");

            migrationBuilder.CreateTable(
                name: "tab_baseline_field",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_task_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属读标任务 ID"),
                    f_category = table.Column<byte>(type: "smallint", nullable: false, comment: "字段分类（BaselineCategory：项目信息/商务数据/目录树等）"),
                    f_field_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "业务字段名，如 price_ceiling"),
                    f_value_json = table.Column<string>(type: "text", nullable: false, comment: "结构化值 JSON"),
                    f_raw_text = table.Column<string>(type: "text", nullable: false, comment: "原文摘要"),
                    f_confidence = table.Column<double>(type: "double precision", nullable: false, comment: "置信度 0~1"),
                    f_status = table.Column<byte>(type: "smallint", nullable: false, comment: "字段状态（Auto/NeedsReview/Confirmed/Edited）"),
                    f_extractor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "来源抽取器：rule / llm / rule+llm"),
                    f_extractor_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "抽取器版本"),
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
                    table.PrimaryKey("PK_tab_baseline_field", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_clause_template",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, comment: "条款文本"),
                    f_mandatory = table.Column<bool>(type: "boolean", nullable: false, comment: "是否强制条款"),
                    f_category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "分类"),
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
                    table.PrimaryKey("PK_tab_clause_template", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_compare_document",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_task_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属比标任务 ID"),
                    f_role = table.Column<byte>(type: "smallint", nullable: false, comment: "文档角色（Bid=投标书 Tender=招标文件）"),
                    f_file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "原始文件名"),
                    f_file_extension = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "文件扩展名（如 .pdf）"),
                    f_file_size = table.Column<long>(type: "bigint", nullable: false, comment: "文件大小（字节）"),
                    f_origin_storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, comment: "原始文件对象存储 key：compare/{taskId}/{docId}/origin.{ext}"),
                    f_parse_status = table.Column<byte>(type: "smallint", nullable: false, comment: "解析状态（DocumentParseStatus）"),
                    f_an_gineer_doc_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "AnGineer 侧文档 id（恢复解析复用，重解析不清空）"),
                    f_parse_error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true, comment: "解析失败原因"),
                    f_parse_progress = table.Column<int>(type: "integer", nullable: true, comment: "AnGineer 解析进度 0~100，终态为 100"),
                    f_parse_stage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "AnGineer 当前管线阶段（source_prep/convert/raw_parse/...）"),
                    f_parse_stage_message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true, comment: "AnGineer 当前阶段消息"),
                    f_parse_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "本次解析开始时间"),
                    f_parse_finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "本次解析结束时间（成功/失败均记录）"),
                    f_ir_storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "内部适配 IR 对象存储 key：compare/{taskId}/{docId}/ir.json"),
                    f_doc_md_storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "content.md 对象存储 key（AnGineer 阅读流 Markdown，LLM 语义层用）"),
                    f_page_count = table.Column<int>(type: "integer", nullable: true, comment: "页数"),
                    f_ocr_low_confidence_ratio = table.Column<double>(type: "double precision", nullable: true, comment: "OCR 低置信块占比（source=ocr 且 confidence<0.5）"),
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
                    table.PrimaryKey("PK_tab_compare_document", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_compare_draft_document",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_draft_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "上传会话 ID（前端生成 UUID，不属于任务记录）"),
                    f_role = table.Column<byte>(type: "smallint", nullable: false, comment: "文档角色（Bid/Tender）"),
                    f_file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "原始文件名"),
                    f_file_extension = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "文件扩展名"),
                    f_file_size = table.Column<long>(type: "bigint", nullable: false, comment: "文件大小（字节）"),
                    f_origin_storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, comment: "原始文件对象存储 key：compare/drafts/{draftId}/{docId}/origin.{ext}"),
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
                    table.PrimaryKey("PK_tab_compare_draft_document", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_compare_task",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "任务名称（项目名）"),
                    f_status = table.Column<byte>(type: "smallint", nullable: false, comment: "任务状态（CompareTaskStatus 状态机）"),
                    f_tender_document_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "招标文件文档 ID（CompareDocument.Role=Tender）"),
                    f_tender_reading_task_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "来源读标任务 ID（创建比标任务时引用读标基准库）"),
                    f_clause_snapshot_json = table.Column<string>(type: "text", nullable: true, comment: "条款清单快照 JSON（元素见 ClauseSnapshotItem），锁定后不可变"),
                    f_clause_drafts_json = table.Column<string>(type: "text", nullable: true, comment: "条款提取草案 JSON，确认前可反复重抽覆盖，确认后转 ClauseSnapshotJson"),
                    f_report_json = table.Column<string>(type: "text", nullable: true, comment: "报告 JSON 缓存（CompareReportDto 序列化），任务 Done 后生成"),
                    f_report_generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "报告生成时间"),
                    f_progress_stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "当前进度阶段标识（如 parsing/comparing）"),
                    f_progress_percent = table.Column<int>(type: "integer", nullable: false, comment: "进度百分比 0~100"),
                    f_progress_message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true, comment: "进度提示消息"),
                    f_failure_reason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true, comment: "Partial/Failed 的原因说明"),
                    f_suggested_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "解析完成后由后端推断的项目名建议"),
                    f_name_edited_by_user = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "用户是否手动编辑过项目名；true 后前端轮询不再自动应用 SuggestedName"),
                    f_pairs_json = table.Column<string>(type: "text", nullable: true, comment: "两两对比对清单 JSON（元素见 ComparePairItem）"),
                    f_auto_compare_on_parse_complete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "解析完成后是否自动进入两两对比"),
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
                    table.PrimaryKey("PK_tab_compare_task", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_evidence_item",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_task_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属比标任务 ID"),
                    f_type = table.Column<byte>(type: "smallint", nullable: false, comment: "证据类型（Similarity/Pricing/Metadata/Clause/Indicator）"),
                    f_severity = table.Column<byte>(type: "smallint", nullable: false, comment: "严重级别（High/Mid/Low）"),
                    f_doc_ids_json = table.Column<string>(type: "text", nullable: false, comment: "涉及文档 ID 列表 JSON 数组"),
                    f_locations_json = table.Column<string>(type: "text", nullable: false, comment: "定位信息 JSON：{ docId, blockIds[] }[]"),
                    f_metrics_json = table.Column<string>(type: "text", nullable: true, comment: "指标 JSON（如 similarity）"),
                    f_title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, comment: "证据标题"),
                    f_description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false, comment: "证据描述"),
                    f_ai_generated = table.Column<bool>(type: "boolean", nullable: false, comment: "是否 AI 生成结论（与算法证据在 UI 上区分）"),
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
                    table.PrimaryKey("PK_tab_evidence_item", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_export_job",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_task_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属比标任务 ID"),
                    f_format = table.Column<byte>(type: "smallint", nullable: false, comment: "导出格式（Pdf/Word）"),
                    f_status = table.Column<byte>(type: "smallint", nullable: false, comment: "导出任务状态"),
                    f_file_storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "导出文件对象存储 key：compare/{taskId}/exports/{jobId}.{pdf|docx}"),
                    f_error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true, comment: "失败原因（导出失败可重试）"),
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
                    table.PrimaryKey("PK_tab_export_job", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_meeting_project",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "项目名称"),
                    f_an_gineer_doc_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "AnGineer 解析产物 doc id（知识库检索用）"),
                    f_doc_ids_json = table.Column<string>(type: "text", nullable: false, comment: "全部施工方案解析产物 doc id 列表 JSON"),
                    f_doc_names_json = table.Column<string>(type: "text", nullable: false, comment: "与 DocIdsJson 对齐的原始文件名列表 JSON（编辑抽屉展示）"),
                    f_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "解析状态：processing / ready / failed"),
                    f_project_info_json = table.Column<string>(type: "text", nullable: false, comment: "LLM 提取的项目信息 JSON"),
                    f_summary = table.Column<string>(type: "text", nullable: false, comment: "LLM 提取的施工方案主要内容"),
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
                    table.PrimaryKey("PK_tab_meeting_project", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_meeting_record",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "会议日期"),
                    f_pre_info_json = table.Column<string>(type: "text", nullable: false, comment: "会前信息 JSON（晨会稿素材）"),
                    f_status = table.Column<byte>(type: "smallint", nullable: false, comment: "会议状态（MeetingStatus）"),
                    f_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "会议开始时间"),
                    f_ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "会议结束时间"),
                    f_speech_draft_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "关联晨会稿 ID"),
                    f_transcript_file = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "会议全程录音存储 key（IFileStorage）"),
                    f_transcript_text = table.Column<string>(type: "text", nullable: true, comment: "转写文本（后台任务回填）"),
                    f_report_file = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "Markdown 报告存储 key"),
                    f_report_error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true, comment: "报告生成失败原因"),
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
                    table.PrimaryKey("PK_tab_meeting_record", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_qa_record",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_meeting_record_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属会议记录 ID"),
                    f_question = table.Column<string>(type: "text", nullable: false, comment: "提问内容"),
                    f_answer = table.Column<string>(type: "text", nullable: false, comment: "回答内容"),
                    f_intent_type = table.Column<byte>(type: "smallint", nullable: false, comment: "意图类型（Knowledge/Chitchat/Meeting）"),
                    f_sources_json = table.Column<string>(type: "text", nullable: false, comment: "证据来源（文件名/页码 JSON 数组）"),
                    f_created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "提问时间"),
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
                    table.PrimaryKey("PK_tab_qa_record", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_source_map_item",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_field_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属基准库字段 ID"),
                    f_block_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "AnGineer block_uid"),
                    f_page_idx = table.Column<int>(type: "integer", nullable: false, comment: "0 基页码，与 IR 一致"),
                    f_bbox_json = table.Column<string>(type: "text", nullable: false, comment: "0~1 归一化矩形 [x0,y0,x1,y1] JSON 数组字符串"),
                    f_text = table.Column<string>(type: "text", nullable: false, comment: "原文片段"),
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
                    table.PrimaryKey("PK_tab_source_map_item", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_speech_draft",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_meeting_record_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属会议记录 ID"),
                    f_content = table.Column<string>(type: "text", nullable: false, comment: "稿件内容"),
                    f_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "草稿状态（draft/generated/confirmed）"),
                    f_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "草稿最后更新时间"),
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
                    table.PrimaryKey("PK_tab_speech_draft", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_tender_reading_document",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_task_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属读标任务 ID"),
                    f_file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "原始文件名"),
                    f_file_extension = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "文件扩展名"),
                    f_file_size = table.Column<long>(type: "bigint", nullable: false, comment: "文件大小（字节）"),
                    f_origin_storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, comment: "原始文件对象存储 key"),
                    f_parse_status = table.Column<byte>(type: "smallint", nullable: false, comment: "解析状态（DocumentParseStatus）"),
                    f_an_gineer_doc_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "AnGineer 侧文档 id"),
                    f_ir_storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "内部适配 IR 对象存储 key"),
                    f_doc_md_storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true, comment: "content.md 对象存储 key"),
                    f_parse_error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true, comment: "解析失败原因"),
                    f_parse_progress = table.Column<int>(type: "integer", nullable: true, comment: "AnGineer 解析进度 0~100"),
                    f_parse_stage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "AnGineer 当前管线阶段"),
                    f_parse_stage_message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true, comment: "AnGineer 当前阶段消息"),
                    f_parse_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "本次解析开始时间"),
                    f_parse_finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "本次解析结束时间"),
                    f_page_count = table.Column<int>(type: "integer", nullable: true, comment: "页数"),
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
                    table.PrimaryKey("PK_tab_tender_reading_document", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_tender_reading_task",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "任务名称"),
                    f_project_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "项目编号（如 ZB-2026-008），抽取后回填"),
                    f_status = table.Column<byte>(type: "smallint", nullable: false, comment: "读标任务状态"),
                    f_progress_stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "当前进度阶段标识"),
                    f_progress_percent = table.Column<int>(type: "integer", nullable: false, comment: "进度百分比 0~100"),
                    f_failure_reason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true, comment: "失败原因"),
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
                    table.PrimaryKey("PK_tab_tender_reading_task", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_unrecognized_face",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_meeting_record_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "所属会议记录 ID"),
                    f_photo_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "裁剪图存储 key（IFileStorage）"),
                    f_confidence = table.Column<double>(type: "double precision", nullable: false, comment: "识别置信度"),
                    f_bbox_json = table.Column<string>(type: "text", nullable: false, comment: "人脸框 [x1,y1,x2,y2] JSON"),
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
                    table.PrimaryKey("PK_tab_unrecognized_face", x => x.f_id);
                });

            migrationBuilder.CreateTable(
                name: "tab_worker_profile",
                columns: table => new
                {
                    f_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "主键Id"),
                    f_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "工人姓名"),
                    f_employee_no = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "工号（业务唯一）"),
                    f_team = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "班组"),
                    f_face_status = table.Column<byte>(type: "smallint", nullable: false, comment: "人脸录入状态（Pending/Enrolled）"),
                    f_face_photos_json = table.Column<string>(type: "text", nullable: false, comment: "人脸照片存储 key JSON 数组"),
                    f_face_enrolled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "人脸录入时间"),
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
                    table.PrimaryKey("PK_tab_worker_profile", x => x.f_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tab_ai_usage_record_f_business",
                table: "tab_ai_usage_record",
                column: "f_business");

            migrationBuilder.CreateIndex(
                name: "IX_tab_ai_usage_record_f_creation_time",
                table: "tab_ai_usage_record",
                column: "f_creation_time");

            migrationBuilder.CreateIndex(
                name: "IX_tab_ai_usage_record_f_success_f_creation_time",
                table: "tab_ai_usage_record",
                columns: new[] { "f_success", "f_creation_time" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_ai_usage_record_f_used_config",
                table: "tab_ai_usage_record",
                column: "f_used_config");

            migrationBuilder.CreateIndex(
                name: "IX_tab_attendance_record_f_meeting_record_id",
                table: "tab_attendance_record",
                column: "f_meeting_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_attendance_record_f_meeting_record_id_f_worker_id",
                table: "tab_attendance_record",
                columns: new[] { "f_meeting_record_id", "f_worker_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_background_jobs_f_is_abandoned_f_next_try_time",
                table: "tab_background_jobs",
                columns: new[] { "f_is_abandoned", "f_next_try_time" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_background_jobs_f_job_name",
                table: "tab_background_jobs",
                column: "f_job_name");

            migrationBuilder.CreateIndex(
                name: "IX_tab_baseline_field_f_task_id_f_category",
                table: "tab_baseline_field",
                columns: new[] { "f_task_id", "f_category" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_baseline_field_f_task_id_f_field_key",
                table: "tab_baseline_field",
                columns: new[] { "f_task_id", "f_field_key" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_compare_document_f_task_id",
                table: "tab_compare_document",
                column: "f_task_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_compare_draft_document_f_draft_id",
                table: "tab_compare_draft_document",
                column: "f_draft_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_compare_task_f_status",
                table: "tab_compare_task",
                column: "f_status");

            migrationBuilder.CreateIndex(
                name: "IX_tab_evidence_item_f_task_id_f_severity",
                table: "tab_evidence_item",
                columns: new[] { "f_task_id", "f_severity" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_evidence_item_f_task_id_f_type",
                table: "tab_evidence_item",
                columns: new[] { "f_task_id", "f_type" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_export_job_f_task_id",
                table: "tab_export_job",
                column: "f_task_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_meeting_record_f_date",
                table: "tab_meeting_record",
                column: "f_date");

            migrationBuilder.CreateIndex(
                name: "IX_tab_meeting_record_f_status",
                table: "tab_meeting_record",
                column: "f_status");

            migrationBuilder.CreateIndex(
                name: "IX_tab_qa_record_f_meeting_record_id",
                table: "tab_qa_record",
                column: "f_meeting_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_source_map_item_f_field_id",
                table: "tab_source_map_item",
                column: "f_field_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_source_map_item_f_field_id_f_page_idx",
                table: "tab_source_map_item",
                columns: new[] { "f_field_id", "f_page_idx" });

            migrationBuilder.CreateIndex(
                name: "IX_tab_speech_draft_f_meeting_record_id",
                table: "tab_speech_draft",
                column: "f_meeting_record_id",
                unique: true,
                filter: "f_is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_tab_tender_reading_document_f_task_id",
                table: "tab_tender_reading_document",
                column: "f_task_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_tender_reading_task_f_project_code",
                table: "tab_tender_reading_task",
                column: "f_project_code");

            migrationBuilder.CreateIndex(
                name: "IX_tab_tender_reading_task_f_status",
                table: "tab_tender_reading_task",
                column: "f_status");

            migrationBuilder.CreateIndex(
                name: "IX_tab_unrecognized_face_f_meeting_record_id",
                table: "tab_unrecognized_face",
                column: "f_meeting_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_tab_worker_profile_f_employee_no",
                table: "tab_worker_profile",
                column: "f_employee_no",
                unique: true,
                filter: "f_is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tab_ai_usage_record");

            migrationBuilder.DropTable(
                name: "tab_attendance_record");

            migrationBuilder.DropTable(
                name: "tab_background_jobs");

            migrationBuilder.DropTable(
                name: "tab_baseline_field");

            migrationBuilder.DropTable(
                name: "tab_clause_template");

            migrationBuilder.DropTable(
                name: "tab_compare_document");

            migrationBuilder.DropTable(
                name: "tab_compare_draft_document");

            migrationBuilder.DropTable(
                name: "tab_compare_task");

            migrationBuilder.DropTable(
                name: "tab_evidence_item");

            migrationBuilder.DropTable(
                name: "tab_export_job");

            migrationBuilder.DropTable(
                name: "tab_meeting_project");

            migrationBuilder.DropTable(
                name: "tab_meeting_record");

            migrationBuilder.DropTable(
                name: "tab_qa_record");

            migrationBuilder.DropTable(
                name: "tab_source_map_item");

            migrationBuilder.DropTable(
                name: "tab_speech_draft");

            migrationBuilder.DropTable(
                name: "tab_tender_reading_document");

            migrationBuilder.DropTable(
                name: "tab_tender_reading_task");

            migrationBuilder.DropTable(
                name: "tab_unrecognized_face");

            migrationBuilder.DropTable(
                name: "tab_worker_profile");
        }
    }
}
