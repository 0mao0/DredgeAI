using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Drafts;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.MeetingBot;
using DredgeAI.BidCompare.TenderReadings;
using Microsoft.EntityFrameworkCore;
using Shiw.Abp.BaseEntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace DredgeAI.BidCompare.EntityFrameworkCore;

public static class BidCompareDbContextModelCreatingExtensions
{
    public static void ConfigureBidCompare(
        this ModelBuilder builder, IShiwDbContextHandler handler)
    {
        Check.NotNull(builder, nameof(builder));

        // CompareTask — 比标任务聚合根
        builder.Entity<CompareTask>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(CompareTask)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.Name)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.Name)))
                .IsRequired()
                .HasMaxLength(128)
                .HasComment("任务名称（项目名）");

            b.Property(x => x.Status)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.Status)))
                .IsRequired()
                .HasComment("任务状态（CompareTaskStatus 状态机）");

            b.Property(x => x.TenderDocumentId)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.TenderDocumentId)))
                .HasComment("招标文件文档 ID（CompareDocument.Role=Tender）");

            b.Property(x => x.TenderReadingTaskId)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.TenderReadingTaskId)))
                .HasComment("来源读标任务 ID（创建比标任务时引用读标基准库）");

            b.Property(x => x.ClauseSnapshotJson)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.ClauseSnapshotJson)))
                .HasColumnType("text")
                .HasComment("条款清单快照 JSON（元素见 ClauseSnapshotItem），锁定后不可变");

            b.Property(x => x.ClauseDraftsJson)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.ClauseDraftsJson)))
                .HasColumnType("text")
                .HasComment("条款提取草案 JSON，确认前可反复重抽覆盖，确认后转 ClauseSnapshotJson");

            b.Property(x => x.ReportJson)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.ReportJson)))
                .HasColumnType("text")
                .HasComment("报告 JSON 缓存（CompareReportDto 序列化），任务 Done 后生成");

            b.Property(x => x.ReportGeneratedAt)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.ReportGeneratedAt)))
                .HasComment("报告生成时间");

            b.Property(x => x.ProgressStage)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.ProgressStage)))
                .IsRequired()
                .HasMaxLength(32)
                .HasComment("当前进度阶段标识（如 parsing/comparing）");

            b.Property(x => x.ProgressPercent)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.ProgressPercent)))
                .HasComment("进度百分比 0~100");

            b.Property(x => x.ProgressMessage)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.ProgressMessage)))
                .HasMaxLength(1024)
                .HasComment("进度提示消息");

            b.Property(x => x.FailureReason)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.FailureReason)))
                .HasMaxLength(2048)
                .HasComment("Partial/Failed 的原因说明");

            b.Property(x => x.SuggestedName)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.SuggestedName)))
                .HasMaxLength(256)
                .HasComment("解析完成后由后端推断的项目名建议");

            b.Property(x => x.NameEditedByUser)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.NameEditedByUser)))
                .HasDefaultValue(false)
                .HasComment("用户是否手动编辑过项目名；true 后前端轮询不再自动应用 SuggestedName");

            b.Property(x => x.PairsJson)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.PairsJson)))
                .HasColumnType("text")
                .HasComment("两两对比对清单 JSON（元素见 ComparePairItem）");

            b.Property(x => x.AutoCompareOnParseComplete)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareTask.AutoCompareOnParseComplete)))
                .HasDefaultValue(true)
                .HasComment("解析完成后是否自动进入两两对比");

            b.HasIndex(x => x.Status);
        });

        // CompareDocument — 比标文档（含解析跟踪）
        builder.Entity<CompareDocument>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(CompareDocument)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.TaskId)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.TaskId)))
                .HasComment("所属比标任务 ID");

            b.Property(x => x.Role)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.Role)))
                .HasComment("文档角色（Bid=投标书 Tender=招标文件）");

            b.Property(x => x.FileName)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.FileName)))
                .IsRequired()
                .HasMaxLength(256)
                .HasComment("原始文件名");

            b.Property(x => x.FileExtension)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.FileExtension)))
                .IsRequired()
                .HasMaxLength(16)
                .HasComment("文件扩展名（如 .pdf）");

            b.Property(x => x.FileSize)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.FileSize)))
                .HasComment("文件大小（字节）");

            b.Property(x => x.OriginStorageKey)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.OriginStorageKey)))
                .IsRequired()
                .HasMaxLength(512)
                .HasComment("原始文件对象存储 key：compare/{taskId}/{docId}/origin.{ext}");

            b.Property(x => x.ParseStatus)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.ParseStatus)))
                .HasComment("解析状态（DocumentParseStatus）");

            b.Property(x => x.AnGineerDocId)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.AnGineerDocId)))
                .HasMaxLength(128)
                .HasComment("AnGineer 侧文档 id（恢复解析复用，重解析不清空）");

            b.Property(x => x.ParseError)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.ParseError)))
                .HasMaxLength(2048)
                .HasComment("解析失败原因");

            b.Property(x => x.ParseProgress)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.ParseProgress)))
                .HasComment("AnGineer 解析进度 0~100，终态为 100");

            b.Property(x => x.ParseStage)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.ParseStage)))
                .HasMaxLength(64)
                .HasComment("AnGineer 当前管线阶段（source_prep/convert/raw_parse/...）");

            b.Property(x => x.ParseStageMessage)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.ParseStageMessage)))
                .HasMaxLength(1024)
                .HasComment("AnGineer 当前阶段消息");

            b.Property(x => x.ParseStartedAt)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.ParseStartedAt)))
                .HasComment("本次解析开始时间");

            b.Property(x => x.ParseFinishedAt)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.ParseFinishedAt)))
                .HasComment("本次解析结束时间（成功/失败均记录）");

            b.Property(x => x.IrStorageKey)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.IrStorageKey)))
                .HasMaxLength(512)
                .HasComment("内部适配 IR 对象存储 key：compare/{taskId}/{docId}/ir.json");

            b.Property(x => x.DocMdStorageKey)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.DocMdStorageKey)))
                .HasMaxLength(512)
                .HasComment("content.md 对象存储 key（AnGineer 阅读流 Markdown，LLM 语义层用）");

            b.Property(x => x.PageCount)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.PageCount)))
                .HasComment("页数");

            b.Property(x => x.OcrLowConfidenceRatio)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDocument.OcrLowConfidenceRatio)))
                .HasComment("OCR 低置信块占比（source=ocr 且 confidence<0.5）");

            b.HasIndex(x => x.TaskId);
        });

        // CompareDraftDocument — 上传会话暂存文件
        builder.Entity<CompareDraftDocument>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(CompareDraftDocument)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.DraftId)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDraftDocument.DraftId)))
                .HasComment("上传会话 ID（前端生成 UUID，不属于任务记录）");

            b.Property(x => x.Role)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDraftDocument.Role)))
                .HasComment("文档角色（Bid/Tender）");

            b.Property(x => x.FileName)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDraftDocument.FileName)))
                .IsRequired()
                .HasMaxLength(256)
                .HasComment("原始文件名");

            b.Property(x => x.FileExtension)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDraftDocument.FileExtension)))
                .IsRequired()
                .HasMaxLength(16)
                .HasComment("文件扩展名");

            b.Property(x => x.FileSize)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDraftDocument.FileSize)))
                .HasComment("文件大小（字节）");

            b.Property(x => x.OriginStorageKey)
                .HasColumnName(handler.FieldNameHandler(nameof(CompareDraftDocument.OriginStorageKey)))
                .IsRequired()
                .HasMaxLength(512)
                .HasComment("原始文件对象存储 key：compare/drafts/{draftId}/{docId}/origin.{ext}");

            b.HasIndex(x => x.DraftId);
        });

        // EvidenceItem — 证据项
        builder.Entity<EvidenceItem>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(EvidenceItem)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.TaskId)
                .HasColumnName(handler.FieldNameHandler(nameof(EvidenceItem.TaskId)))
                .HasComment("所属比标任务 ID");

            b.Property(x => x.Type)
                .HasColumnName(handler.FieldNameHandler(nameof(EvidenceItem.Type)))
                .HasComment("证据类型（Similarity/Pricing/Metadata/Clause/Indicator）");

            b.Property(x => x.Severity)
                .HasColumnName(handler.FieldNameHandler(nameof(EvidenceItem.Severity)))
                .HasComment("严重级别（High/Mid/Low）");

            b.Property(x => x.DocIdsJson)
                .HasColumnName(handler.FieldNameHandler(nameof(EvidenceItem.DocIdsJson)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("涉及文档 ID 列表 JSON 数组");

            b.Property(x => x.LocationsJson)
                .HasColumnName(handler.FieldNameHandler(nameof(EvidenceItem.LocationsJson)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("定位信息 JSON：{ docId, blockIds[] }[]");

            b.Property(x => x.MetricsJson)
                .HasColumnName(handler.FieldNameHandler(nameof(EvidenceItem.MetricsJson)))
                .HasColumnType("text")
                .HasComment("指标 JSON（如 similarity）");

            b.Property(x => x.Title)
                .HasColumnName(handler.FieldNameHandler(nameof(EvidenceItem.Title)))
                .IsRequired()
                .HasMaxLength(512)
                .HasComment("证据标题");

            b.Property(x => x.Description)
                .HasColumnName(handler.FieldNameHandler(nameof(EvidenceItem.Description)))
                .IsRequired()
                .HasMaxLength(4000)
                .HasComment("证据描述");

            b.Property(x => x.AiGenerated)
                .HasColumnName(handler.FieldNameHandler(nameof(EvidenceItem.AiGenerated)))
                .HasComment("是否 AI 生成结论（与算法证据在 UI 上区分）");

            b.HasIndex(x => new { x.TaskId, x.Type });
            b.HasIndex(x => new { x.TaskId, x.Severity });
        });

        // ClauseTemplate — 个人条款库模板
        builder.Entity<ClauseTemplate>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(ClauseTemplate)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.Text)
                .HasColumnName(handler.FieldNameHandler(nameof(ClauseTemplate.Text)))
                .IsRequired()
                .HasMaxLength(2000)
                .HasComment("条款文本");

            b.Property(x => x.Mandatory)
                .HasColumnName(handler.FieldNameHandler(nameof(ClauseTemplate.Mandatory)))
                .HasComment("是否强制条款");

            b.Property(x => x.Category)
                .HasColumnName(handler.FieldNameHandler(nameof(ClauseTemplate.Category)))
                .HasMaxLength(64)
                .HasComment("分类");
        });

        // ExportJob — 导出任务句柄（异步导出）
        builder.Entity<ExportJob>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(ExportJob)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.TaskId)
                .HasColumnName(handler.FieldNameHandler(nameof(ExportJob.TaskId)))
                .HasComment("所属比标任务 ID");

            b.Property(x => x.Format)
                .HasColumnName(handler.FieldNameHandler(nameof(ExportJob.Format)))
                .HasComment("导出格式（Pdf/Word）");

            b.Property(x => x.Status)
                .HasColumnName(handler.FieldNameHandler(nameof(ExportJob.Status)))
                .HasComment("导出任务状态");

            b.Property(x => x.FileStorageKey)
                .HasColumnName(handler.FieldNameHandler(nameof(ExportJob.FileStorageKey)))
                .HasMaxLength(512)
                .HasComment("导出文件对象存储 key：compare/{taskId}/exports/{jobId}.{pdf|docx}");

            b.Property(x => x.Error)
                .HasColumnName(handler.FieldNameHandler(nameof(ExportJob.Error)))
                .HasMaxLength(2048)
                .HasComment("失败原因（导出失败可重试）");

            b.HasIndex(x => x.TaskId);
        });

        // AiUsageRecord — LLM 调用用量记录（ai-gateway 上报）
        builder.Entity<AiUsageRecord>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(AiUsageRecord)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.Business)
                .HasColumnName(handler.FieldNameHandler(nameof(AiUsageRecord.Business)))
                .IsRequired()
                .HasMaxLength(64)
                .HasComment("业务场景标识");

            b.Property(x => x.UsedConfig)
                .HasColumnName(handler.FieldNameHandler(nameof(AiUsageRecord.UsedConfig)))
                .IsRequired()
                .HasMaxLength(128)
                .HasComment("使用的模型配置名");

            b.Property(x => x.UsedModel)
                .HasColumnName(handler.FieldNameHandler(nameof(AiUsageRecord.UsedModel)))
                .IsRequired()
                .HasMaxLength(128)
                .HasComment("实际调用模型名");

            b.Property(x => x.InputTokens)
                .HasColumnName(handler.FieldNameHandler(nameof(AiUsageRecord.InputTokens)))
                .HasComment("输入 token 数");

            b.Property(x => x.OutputTokens)
                .HasColumnName(handler.FieldNameHandler(nameof(AiUsageRecord.OutputTokens)))
                .HasComment("输出 token 数");

            b.Property(x => x.TotalTokens)
                .HasColumnName(handler.FieldNameHandler(nameof(AiUsageRecord.TotalTokens)))
                .HasComment("总 token 数");

            b.Property(x => x.FinishReason)
                .HasColumnName(handler.FieldNameHandler(nameof(AiUsageRecord.FinishReason)))
                .HasComment("模型结束原因");

            b.Property(x => x.Attempts)
                .HasColumnName(handler.FieldNameHandler(nameof(AiUsageRecord.Attempts)))
                .HasComment("调用尝试次数（含重试）");

            b.Property(x => x.LatencySeconds)
                .HasColumnName(handler.FieldNameHandler(nameof(AiUsageRecord.LatencySeconds)))
                .HasComment("调用耗时（秒）");

            b.Property(x => x.CircuitBreakerState)
                .HasColumnName(handler.FieldNameHandler(nameof(AiUsageRecord.CircuitBreakerState)))
                .HasComment("熔断器状态");

            b.Property(x => x.Success)
                .HasColumnName(handler.FieldNameHandler(nameof(AiUsageRecord.Success)))
                .HasComment("是否成功");

            b.Property(x => x.ErrorType)
                .HasColumnName(handler.FieldNameHandler(nameof(AiUsageRecord.ErrorType)))
                .HasComment("错误类型");

            b.Property(x => x.ErrorMessage)
                .HasColumnName(handler.FieldNameHandler(nameof(AiUsageRecord.ErrorMessage)))
                .HasMaxLength(2048)
                .HasComment("错误信息");

            b.Property(x => x.TextPreview)
                .HasColumnName(handler.FieldNameHandler(nameof(AiUsageRecord.TextPreview)))
                .HasMaxLength(512)
                .HasComment("输入文本预览");

            b.HasIndex(x => x.CreationTime);
            b.HasIndex(x => x.UsedConfig);
            b.HasIndex(x => x.Business);
            b.HasIndex(x => new { x.Success, x.CreationTime });
        });

        // TenderReadingTask — 读标任务聚合根
        builder.Entity<TenderReadingTask>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(TenderReadingTask)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.Name)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingTask.Name)))
                .IsRequired()
                .HasMaxLength(128)
                .HasComment("任务名称");

            b.Property(x => x.ProjectCode)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingTask.ProjectCode)))
                .HasMaxLength(64)
                .HasComment("项目编号（如 ZB-2026-008），抽取后回填");

            b.Property(x => x.Status)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingTask.Status)))
                .IsRequired()
                .HasComment("读标任务状态");

            b.Property(x => x.ProgressStage)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingTask.ProgressStage)))
                .IsRequired()
                .HasMaxLength(32)
                .HasComment("当前进度阶段标识");

            b.Property(x => x.ProgressPercent)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingTask.ProgressPercent)))
                .HasComment("进度百分比 0~100");

            b.Property(x => x.FailureReason)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingTask.FailureReason)))
                .HasMaxLength(2048)
                .HasComment("失败原因");

            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.ProjectCode);
        });

        // TenderReadingDocument — 读标任务关联文档
        builder.Entity<TenderReadingDocument>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(TenderReadingDocument)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.TaskId)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.TaskId)))
                .HasComment("所属读标任务 ID");

            b.Property(x => x.FileName)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.FileName)))
                .IsRequired()
                .HasMaxLength(256)
                .HasComment("原始文件名");

            b.Property(x => x.FileExtension)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.FileExtension)))
                .IsRequired()
                .HasMaxLength(16)
                .HasComment("文件扩展名");

            b.Property(x => x.FileSize)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.FileSize)))
                .HasComment("文件大小（字节）");

            b.Property(x => x.OriginStorageKey)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.OriginStorageKey)))
                .IsRequired()
                .HasMaxLength(512)
                .HasComment("原始文件对象存储 key");

            b.Property(x => x.ParseStatus)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.ParseStatus)))
                .HasComment("解析状态（DocumentParseStatus）");

            b.Property(x => x.AnGineerDocId)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.AnGineerDocId)))
                .HasMaxLength(128)
                .HasComment("AnGineer 侧文档 id");

            b.Property(x => x.IrStorageKey)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.IrStorageKey)))
                .HasMaxLength(512)
                .HasComment("内部适配 IR 对象存储 key");

            b.Property(x => x.DocMdStorageKey)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.DocMdStorageKey)))
                .HasMaxLength(512)
                .HasComment("content.md 对象存储 key");

            b.Property(x => x.ParseError)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.ParseError)))
                .HasMaxLength(2048)
                .HasComment("解析失败原因");

            b.Property(x => x.ParseProgress)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.ParseProgress)))
                .HasComment("AnGineer 解析进度 0~100");

            b.Property(x => x.ParseStage)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.ParseStage)))
                .HasMaxLength(64)
                .HasComment("AnGineer 当前管线阶段");

            b.Property(x => x.ParseStageMessage)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.ParseStageMessage)))
                .HasMaxLength(1024)
                .HasComment("AnGineer 当前阶段消息");

            b.Property(x => x.ParseStartedAt)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.ParseStartedAt)))
                .HasComment("本次解析开始时间");

            b.Property(x => x.ParseFinishedAt)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.ParseFinishedAt)))
                .HasComment("本次解析结束时间");

            b.Property(x => x.PageCount)
                .HasColumnName(handler.FieldNameHandler(nameof(TenderReadingDocument.PageCount)))
                .HasComment("页数");

            b.HasIndex(x => x.TaskId);
        });

        // BaselineField — 基准库字段
        builder.Entity<BaselineField>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(BaselineField)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.TaskId)
                .HasColumnName(handler.FieldNameHandler(nameof(BaselineField.TaskId)))
                .HasComment("所属读标任务 ID");

            b.Property(x => x.Category)
                .HasColumnName(handler.FieldNameHandler(nameof(BaselineField.Category)))
                .HasComment("字段分类（BaselineCategory：项目信息/商务数据/目录树等）");

            b.Property(x => x.FieldKey)
                .HasColumnName(handler.FieldNameHandler(nameof(BaselineField.FieldKey)))
                .IsRequired()
                .HasMaxLength(128)
                .HasComment("业务字段名，如 price_ceiling");

            b.Property(x => x.ValueJson)
                .HasColumnName(handler.FieldNameHandler(nameof(BaselineField.ValueJson)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("结构化值 JSON");

            b.Property(x => x.RawText)
                .HasColumnName(handler.FieldNameHandler(nameof(BaselineField.RawText)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("原文摘要");

            b.Property(x => x.Confidence)
                .HasColumnName(handler.FieldNameHandler(nameof(BaselineField.Confidence)))
                .IsRequired()
                .HasComment("置信度 0~1");

            b.Property(x => x.Status)
                .HasColumnName(handler.FieldNameHandler(nameof(BaselineField.Status)))
                .IsRequired()
                .HasComment("字段状态（Auto/NeedsReview/Confirmed/Edited）");

            b.Property(x => x.Extractor)
                .HasColumnName(handler.FieldNameHandler(nameof(BaselineField.Extractor)))
                .IsRequired()
                .HasMaxLength(32)
                .HasComment("来源抽取器：rule / llm / rule+llm");

            b.Property(x => x.ExtractorVersion)
                .HasColumnName(handler.FieldNameHandler(nameof(BaselineField.ExtractorVersion)))
                .IsRequired()
                .HasMaxLength(32)
                .HasComment("抽取器版本");

            b.HasIndex(x => new { x.TaskId, x.Category });
            b.HasIndex(x => new { x.TaskId, x.FieldKey });
        });

        // SourceMapItem — 字段原文锚点
        builder.Entity<SourceMapItem>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(SourceMapItem)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.FieldId)
                .HasColumnName(handler.FieldNameHandler(nameof(SourceMapItem.FieldId)))
                .HasComment("所属基准库字段 ID");

            b.Property(x => x.BlockId)
                .HasColumnName(handler.FieldNameHandler(nameof(SourceMapItem.BlockId)))
                .IsRequired()
                .HasMaxLength(128)
                .HasComment("AnGineer block_uid");

            b.Property(x => x.PageIdx)
                .HasColumnName(handler.FieldNameHandler(nameof(SourceMapItem.PageIdx)))
                .HasComment("0 基页码，与 IR 一致");

            b.Property(x => x.BboxJson)
                .HasColumnName(handler.FieldNameHandler(nameof(SourceMapItem.BboxJson)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("0~1 归一化矩形 [x0,y0,x1,y1] JSON 数组字符串");

            b.Property(x => x.Text)
                .HasColumnName(handler.FieldNameHandler(nameof(SourceMapItem.Text)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("原文片段");

            b.HasIndex(x => x.FieldId);
            b.HasIndex(x => new { x.FieldId, x.PageIdx });
        });

        // MeetingRecord — AI 晨会记录聚合根
        builder.Entity<MeetingRecord>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(MeetingRecord)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.Date)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingRecord.Date)))
                .HasComment("会议日期");

            b.Property(x => x.PreInfoJson)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingRecord.PreInfoJson)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("会前信息 JSON（晨会稿素材）");

            b.Property(x => x.Status)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingRecord.Status)))
                .IsRequired()
                .HasComment("会议状态（MeetingStatus）");

            b.Property(x => x.StartedAt)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingRecord.StartedAt)))
                .HasComment("会议开始时间");

            b.Property(x => x.EndedAt)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingRecord.EndedAt)))
                .HasComment("会议结束时间");

            b.Property(x => x.SpeechDraftId)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingRecord.SpeechDraftId)))
                .HasComment("关联晨会稿 ID");

            b.Property(x => x.TranscriptFile)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingRecord.TranscriptFile)))
                .HasMaxLength(512)
                .HasComment("会议全程录音存储 key（IFileStorage）");

            b.Property(x => x.TranscriptText)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingRecord.TranscriptText)))
                .HasColumnType("text")
                .HasComment("转写文本（后台任务回填）");

            b.Property(x => x.ReportFile)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingRecord.ReportFile)))
                .HasMaxLength(512)
                .HasComment("Markdown 报告存储 key");

            b.Property(x => x.ReportError)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingRecord.ReportError)))
                .HasMaxLength(2048)
                .HasComment("报告生成失败原因");

            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.Date);
        });

        // SpeechDraft — 晨会稿草稿
        builder.Entity<SpeechDraft>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(SpeechDraft)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.MeetingRecordId)
                .HasColumnName(handler.FieldNameHandler(nameof(SpeechDraft.MeetingRecordId)))
                .HasComment("所属会议记录 ID");

            b.Property(x => x.Content)
                .HasColumnName(handler.FieldNameHandler(nameof(SpeechDraft.Content)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("稿件内容");

            b.Property(x => x.Status)
                .HasColumnName(handler.FieldNameHandler(nameof(SpeechDraft.Status)))
                .IsRequired()
                .HasMaxLength(16)
                .HasComment("草稿状态（draft/generated/confirmed）");

            b.Property(x => x.UpdatedAt)
                .HasColumnName(handler.FieldNameHandler(nameof(SpeechDraft.UpdatedAt)))
                .HasComment("草稿最后更新时间");

            var idx = b.HasIndex(x => x.MeetingRecordId).IsUnique();
            if (typeof(ISoftDelete).IsAssignableFrom(typeof(SpeechDraft)))
            {
                idx.HasFilter($"{handler.FieldNameHandler(nameof(ISoftDelete.IsDeleted))} = false");
            }
        });

        // AttendanceRecord — 点名记录
        builder.Entity<AttendanceRecord>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(AttendanceRecord)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.MeetingRecordId)
                .HasColumnName(handler.FieldNameHandler(nameof(AttendanceRecord.MeetingRecordId)))
                .HasComment("所属会议记录 ID");

            b.Property(x => x.WorkerId)
                .HasColumnName(handler.FieldNameHandler(nameof(AttendanceRecord.WorkerId)))
                .HasComment("识别到的工人 ID，未识别为 null");

            b.Property(x => x.Name)
                .HasColumnName(handler.FieldNameHandler(nameof(AttendanceRecord.Name)))
                .IsRequired()
                .HasMaxLength(64)
                .HasComment("姓名（未识别为空串）");

            b.Property(x => x.Team)
                .HasColumnName(handler.FieldNameHandler(nameof(AttendanceRecord.Team)))
                .HasMaxLength(64)
                .HasComment("班组");

            b.Property(x => x.Status)
                .HasColumnName(handler.FieldNameHandler(nameof(AttendanceRecord.Status)))
                .IsRequired()
                .HasComment("出勤状态（Present/Absent/Late/Unrecognized）");

            b.Property(x => x.Confidence)
                .HasColumnName(handler.FieldNameHandler(nameof(AttendanceRecord.Confidence)))
                .IsRequired()
                .HasComment("人脸识别置信度");

            b.Property(x => x.Bbox)
                .HasColumnName(handler.FieldNameHandler(nameof(AttendanceRecord.Bbox)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("人脸框 [x1,y1,x2,y2] JSON，未识别人脸去重用，无坐标为 \"[]\"");

            b.HasIndex(x => x.MeetingRecordId);
            b.HasIndex(x => new { x.MeetingRecordId, x.WorkerId });
        });

        // MeetingProject — AI 晨会施工项目
        builder.Entity<MeetingProject>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(MeetingProject)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.Name)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingProject.Name)))
                .IsRequired()
                .HasMaxLength(128)
                .HasComment("项目名称");

            b.Property(x => x.AnGineerDocId)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingProject.AnGineerDocId)))
                .HasMaxLength(128)
                .HasComment("AnGineer 解析产物 doc id（知识库检索用）");

            b.Property(x => x.DocIdsJson)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingProject.DocIdsJson)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("全部施工方案解析产物 doc id 列表 JSON");

            b.Property(x => x.DocNamesJson)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingProject.DocNamesJson)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("与 DocIdsJson 对齐的原始文件名列表 JSON（编辑抽屉展示）");

            b.Property(x => x.Status)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingProject.Status)))
                .IsRequired()
                .HasMaxLength(16)
                .HasComment("解析状态：processing / ready / failed");

            b.Property(x => x.ProjectInfoJson)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingProject.ProjectInfoJson)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("LLM 提取的项目信息 JSON");

            b.Property(x => x.Summary)
                .HasColumnName(handler.FieldNameHandler(nameof(MeetingProject.Summary)))
                .HasColumnType("text")
                .HasComment("LLM 提取的施工方案主要内容");
        });

        // UnrecognizedFace — 点名未识别人脸裁剪图
        builder.Entity<UnrecognizedFace>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(UnrecognizedFace)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.MeetingRecordId)
                .HasColumnName(handler.FieldNameHandler(nameof(UnrecognizedFace.MeetingRecordId)))
                .HasComment("所属会议记录 ID");

            b.Property(x => x.PhotoKey)
                .HasColumnName(handler.FieldNameHandler(nameof(UnrecognizedFace.PhotoKey)))
                .IsRequired()
                .HasMaxLength(256)
                .HasComment("裁剪图存储 key（IFileStorage）");

            b.Property(x => x.Confidence)
                .HasColumnName(handler.FieldNameHandler(nameof(UnrecognizedFace.Confidence)))
                .IsRequired()
                .HasComment("识别置信度");

            b.Property(x => x.BboxJson)
                .HasColumnName(handler.FieldNameHandler(nameof(UnrecognizedFace.BboxJson)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("人脸框 [x1,y1,x2,y2] JSON");

            b.HasIndex(x => x.MeetingRecordId);
        });

        // QaRecord — 会议问答记录
        builder.Entity<QaRecord>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(QaRecord)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.MeetingRecordId)
                .HasColumnName(handler.FieldNameHandler(nameof(QaRecord.MeetingRecordId)))
                .HasComment("所属会议记录 ID");

            b.Property(x => x.Question)
                .HasColumnName(handler.FieldNameHandler(nameof(QaRecord.Question)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("提问内容");

            b.Property(x => x.Answer)
                .HasColumnName(handler.FieldNameHandler(nameof(QaRecord.Answer)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("回答内容");

            b.Property(x => x.IntentType)
                .HasColumnName(handler.FieldNameHandler(nameof(QaRecord.IntentType)))
                .IsRequired()
                .HasComment("意图类型（Knowledge/Chitchat/Meeting）");

            b.Property(x => x.SourcesJson)
                .HasColumnName(handler.FieldNameHandler(nameof(QaRecord.SourcesJson)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("证据来源（文件名/页码 JSON 数组）");

            b.Property(x => x.CreatedAt)
                .HasColumnName(handler.FieldNameHandler(nameof(QaRecord.CreatedAt)))
                .HasComment("提问时间");

            b.HasIndex(x => x.MeetingRecordId);
        });

        // WorkerProfile — 工人档案（花名册 + 人脸库）
        builder.Entity<WorkerProfile>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{BidCompareDbProperties.DbTablePrefix}{nameof(WorkerProfile)}"),
                BidCompareDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.Name)
                .HasColumnName(handler.FieldNameHandler(nameof(WorkerProfile.Name)))
                .IsRequired()
                .HasMaxLength(64)
                .HasComment("工人姓名");

            b.Property(x => x.EmployeeNo)
                .HasColumnName(handler.FieldNameHandler(nameof(WorkerProfile.EmployeeNo)))
                .IsRequired()
                .HasMaxLength(32)
                .HasComment("工号（业务唯一）");

            b.Property(x => x.Team)
                .HasColumnName(handler.FieldNameHandler(nameof(WorkerProfile.Team)))
                .HasMaxLength(64)
                .HasComment("班组");

            b.Property(x => x.FaceStatus)
                .HasColumnName(handler.FieldNameHandler(nameof(WorkerProfile.FaceStatus)))
                .IsRequired()
                .HasComment("人脸录入状态（Pending/Enrolled）");

            b.Property(x => x.FacePhotosJson)
                .HasColumnName(handler.FieldNameHandler(nameof(WorkerProfile.FacePhotosJson)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("人脸照片存储 key JSON 数组");

            b.Property(x => x.FaceEnrolledAt)
                .HasColumnName(handler.FieldNameHandler(nameof(WorkerProfile.FaceEnrolledAt)))
                .HasComment("人脸录入时间");

            var employeeNoIndex = b.HasIndex(x => x.EmployeeNo).IsUnique();
            if (typeof(ISoftDelete).IsAssignableFrom(typeof(WorkerProfile)))
            {
                employeeNoIndex.HasFilter($"{handler.FieldNameHandler(nameof(ISoftDelete.IsDeleted))} = false");
            }
        });
    }
}
