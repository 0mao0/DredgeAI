# 比标 ABP 后端服务 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为「AI 投标-比标」模块交付 ABP Framework (.NET 8) 后端主服务：比标任务状态机（`parsing→parsed→(awaitingClauses)→comparing→analyzing→done`，异常态 `failed/partial`）、文档上传与 S3 存储、AnGIneer 解析产物（`doc_blocks_graph.jsonl` + `doc_blocks_graph_meta.json` + `content.md` + `images/`）接收、**按 v2 消费要求映射为内部适配 IR**、校验与落库、调用 Python 算法服务产出确定性证据、调用 LLM 做条款提取/响应判定/指标抽取、报告 JSON 组装与 Word/PDF 异步导出。API 逐字符合设计 spec §6 与《ABP接口响应格式标准》；IR 字段语义遵循 `docs/superpowers/plans/dredgeai-consume-angineer-requirements.md`（2026-08-07 v2，下称 v2 文档）。

**Architecture:** 单一 ABP 分层解决方案 `DredgeAI.BidCompare`（Domain.Shared / Domain / Application.Contracts / Application / EntityFrameworkCore / HttpApi / HttpApi.Host）。API 用**显式 Controller** 暴露 `/api/compare/*` 精确路由（AppService 标 `[RemoteService(false)]` 不走约定式路由）。异步阶段（解析/比对/AI 分析/导出）全部走 ABP Background Jobs，前端轮询任务状态。外部依赖（AnGIneer、算法服务、LLM、S3、PDF 转换）全部接口化，实现在 HttpApi.Host，测试全部用 Fake。

**Tech Stack:** .NET 8 · ABP Framework 8.x（`abp new -t app -u none`）· EF Core 8 + PostgreSQL（Npgsql）· ABP Background Jobs · AWSSDK.S3（MinIO）· HttpClient + OpenAI 兼容协议 · DocumentFormat.OpenXml · LibreOffice headless（soffice）· xUnit + Shouldly（ABP 测试基座，SQLite in-memory）。

**假设（已在设计中拍板）:**

1. ABP Framework 8.x + .NET 8，新解决方案 `DredgeAI.BidCompare`，从 `abp new` 脚手架开始（不含 UI 的模板），EF Core + PostgreSQL，后台任务用 ABP Background Jobs（解析/分析/导出异步化）。
2. 对象存储：S3 兼容（MinIO），统一用 **AWSSDK.S3**（不混用 MinIO SDK），存原始文件 / IR 包 / 导出文件。
3. 解析对接：提供方为 **AnGIneer**（v2 决策：直接消费其 `parsed/` 产物，不再要求 ir.json，也不做薄转化层）。假设 AnGIneer 提供 HTTP API（提交文档 → 轮询 → 下载产物 zip 包：`doc_blocks_graph.jsonl` + `doc_blocks_graph_meta.json` + `content.md` + `images/`），用 C# 接口 `IAnGineerClient` 做 adapter 隔离，形态变化只改实现；产物由 `AnGineerIrMapper` 按 v2 §2/§3 映射为内部适配 IR 后校验、落库。
4. LLM：OpenAI 兼容协议（HttpClient + 可配置 endpoint/model/key），抽象 `ILlmGateway`。
5. 算法服务：独立 Python 服务（不在本计划范围），本服务通过 HttpClient 调 `POST /analyze/similarity|pricing|metadata`，请求为多份**内部适配 IR**（bbox 0~1 归一化、blockId=block_uid、source/confidence nullable），响应为 Evidence 数组；Evidence 字段名见 spec §6.1，逐字遵守。
6. 报告导出：Word 用 OpenXML SDK 基于 docx 模板填充；PDF 用 LibreOffice headless（`soffice --convert-to pdf`）转换；导出异步（Background Job + 轮询下载链接）。
7. 测试：xUnit + Shouldly（ABP 测试基座），TDD；外部依赖（AnGIneer/LLM/算法服务/S3/PDF 转换）全部用接口 + fake。
8. 分期：P1 = 脚手架、任务与文档管理、IR 接收与存储、AnGIneer adapter、调算法服务、证据持久化与查询 API、相似度矩阵 API；P2 = 条款库 CRUD、条款提取/确认快照、LLM 响应判定与指标抽取、报告 JSON 组装、Word/PDF 导出。

**契约裁决（spec 与 ABP 标准冲突处的统一口径，全计划一致执行）:**

- **字段名逐字遵守 spec §6.1**（`createdAt`、`tenderDocId`、`clauseSnapshot`、`docIds`、`aiGenerated`、`blockIds` 等）。因此 `CompareTaskDto`/`EvidenceDto` 等继承 `EntityDto<Guid>` 并显式声明 `CreatedAt`，**不**继承 `AuditedEntityDto`（避免 `creationTime` 与 `createdAt` 并存）。
- **IR 形态按 2026-08-07 v2 文档执行**（`docs/superpowers/plans/dredgeai-consume-angineer-requirements.md`，取代 spec §4 的 ir.json 交付契约）：DredgeAI 直接消费 AnGIneer `doc_blocks_graph` 产物；后端在解析阶段经 `AnGineerIrMapper` 映射出一份**内部适配 IR**（字段名 `blockId/pageIdx/bbox/type/text/textLevel/source/confidence/table.html/imgPath/outline/meta/pages`，bbox 0~1 归一化、`blockId`=block_uid、`source/confidence` nullable），存为 `compare/{taskId}/{docId}/ir.json`（内部产物，非跨系统交付物），供算法服务请求与前端 `GET /ir/{docId}` 使用；`IrValidator` 校验该内部形态。
- **枚举按《ABP接口响应格式标准》以整型序列化**（spec §6.1 中的 `'similarity'|...` 字符串联合类型仅表语义）。Task 1 会在 Host 中移除 `JsonStringEnumConverter` 以保证 int 输出。
- **JSON 快照字段（ClauseSnapshotJson / ReportJson / DocIdsJson / LocationsJson / MetricsJson）以 text 列存储**，不使用 jsonb——测试库为 SQLite in-memory，且原型期无 JSON 内查询需求；后续如需 JSONB 查询再单独迁移。
- **spec §6 未列出但流程必需的补充路由**（在对应 Task 中实现，自查表标注「补充」）：
  - `DELETE /api/compare/tasks/{id}` — spec §7.1 任务列表操作列有「删除」；
  - `PUT /api/compare/clause-templates/{id}`、`DELETE /api/compare/clause-templates/{id}` — 条款库「用户手动维护」需要完整 CRUD；
  - `GET /api/compare/tasks/{id}/exports/{jobId}` — spec §6.2「导出异步化，前端轮询获取下载链接」需要轮询入口。
- 权限/认证沿用模板默认（OpenIddict 已配置），本计划不定义业务权限点，Controller 不加 `[Authorize]`（原型期匿名可访问），上线前单独补权限设计（列入 Task 15 收尾检查项）。

**解决方案结构（Task 1 生成，后续 Task 引用路径均以 `backend/DredgeAI.BidCompare` 为根）:**

```
backend/DredgeAI.BidCompare/
├── DredgeAI.BidCompare.sln
├── src/
│   ├── DredgeAI.BidCompare.Domain.Shared/        # 枚举、错误码、本地化
│   ├── DredgeAI.BidCompare.Domain/               # 实体、领域服务、外部依赖接口
│   ├── DredgeAI.BidCompare.Application.Contracts/# DTO、AppService 接口
│   ├── DredgeAI.BidCompare.Application/          # AppService、Background Jobs、ReportBuilder
│   ├── DredgeAI.BidCompare.EntityFrameworkCore/  # DbContext、映射、迁移
│   ├── DredgeAI.BidCompare.HttpApi/              # 显式 Controller（精确路由）
│   ├── DredgeAI.BidCompare.HttpApi.Host/         # 宿主 + S3/AnGineer/Algo/LLM/OpenXML/LibreOffice 实现
│   └── DredgeAI.BidCompare.DbMigrator/           # 模板自带
└── test/
    ├── DredgeAI.BidCompare.TestBase/             # 测试基座 + 全部 Fake
    ├── DredgeAI.BidCompare.Domain.Tests/
    ├── DredgeAI.BidCompare.Application.Tests/
    └── DredgeAI.BidCompare.EntityFrameworkCore.Tests/
```

**状态机（与 spec §5 数据流严格一致）:**

```
Parsing ──全部文档解析成功──▶ Parsed ──┬─有招标文件且无条款快照─▶ AwaitingClauses ──PUT clauses──▶ Comparing
   │                                 └─无招标文件/已有快照──▶ Comparing
   ├─部分失败（≥1 成功）──▶ Partial（标记态，后续流转与 Parsed 相同，spec §9「其余文档照常对比」）
   └─全部失败──▶ Failed
Comparing ──证据落库完成──▶ Analyzing ──AI 分析完成/降级──▶ Done
Comparing/Analyzing 阶段算法服务不可用 ──▶ Failed（原因入 FailureReason，spec §9 不静默降级）
```

---

## Task 1 【P1】解决方案脚手架与基础设施验证

**Files:**
- Create: `backend/DredgeAI.BidCompare/`（整个解决方案，由 `abp new` 生成）
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/appsettings.json`
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.DbMigrator/appsettings.json`
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/BidCompareHttpApiHostModule.cs`

**Steps:**

- [ ] **Step 1: 安装/更新 ABP CLI 并生成解决方案**

  ```bash
  dotnet tool install --global Volo.Abp.Cli || dotnet tool update --global Volo.Abp.Cli
  cd D:/AI/DredgeAI
  abp new DredgeAI.BidCompare -t app -u none -d ef -dbms PostgreSQL -o backend/DredgeAI.BidCompare
  ```

  预期：`backend/DredgeAI.BidCompare/DredgeAI.BidCompare.sln` 生成，包含上述「解决方案结构」中的全部项目。

- [ ] **Step 2: 配置 PostgreSQL 连接串并跑通迁移**

  修改 `src/DredgeAI.BidCompare.HttpApi.Host/appsettings.json` 与 `src/DredgeAI.BidCompare.DbMigrator/appsettings.json` 的 `ConnectionStrings:Default` 为本机 PostgreSQL：

  ```json
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=BidCompare;User ID=postgres;Password=postgres"
  }
  ```

  ```bash
  cd backend/DredgeAI.BidCompare
  dotnet build DredgeAI.BidCompare.sln
  dotnet run --project src/DredgeAI.BidCompare.DbMigrator
  ```

  预期：build 0 error；DbMigrator 输出 `Successfully migrated host database`，数据库 `BidCompare` 创建并含 ABP 基础表。

- [ ] **Step 3: 保证枚举按整型序列化（ABP 标准 §1）**

  在 `src/DredgeAI.BidCompare.HttpApi.Host/BidCompareHttpApiHostModule.cs` 的 `ConfigureServices` 方法**末尾**追加：

  ```csharp
  Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
  {
      for (var i = options.JsonSerializerOptions.Converters.Count - 1; i >= 0; i--)
      {
          if (options.JsonSerializerOptions.Converters[i] is System.Text.Json.Serialization.JsonStringEnumConverter)
          {
              options.JsonSerializerOptions.Converters.RemoveAt(i);
          }
      }
  });
  ```

  预期：后续所有枚举字段（`status`、`type`、`severity`、`role`、`format` 等）序列化为 int。Task 15 契约测试会固化此行为。

- [ ] **Step 4: 跑通模板测试并启动宿主冒烟**

  ```bash
  dotnet test DredgeAI.BidCompare.sln
  dotnet run --project src/DredgeAI.BidCompare.HttpApi.Host &
  curl -s http://localhost:44342/swagger/index.html -o /dev/null -w "%{http_code}"
  kill %1
  ```

  预期：模板测试全绿；swagger 返回 200。

- [ ] **Step 5: 提交脚手架**

  ```bash
  cd D:/AI/DredgeAI
  git add backend/DredgeAI.BidCompare
  git commit -m "chore(backend): scaffold DredgeAI.BidCompare ABP solution (app template, no UI, EF Core + PostgreSQL)"
  ```

---

## Task 2 【P1】Domain.Shared：枚举、错误码、本地化

**Files:**
- Create: `src/DredgeAI.BidCompare.Domain.Shared/CompareTasks/CompareTaskStatus.cs`
- Create: `src/DredgeAI.BidCompare.Domain.Shared/Documents/DocumentRole.cs`
- Create: `src/DredgeAI.BidCompare.Domain.Shared/Documents/DocumentParseStatus.cs`
- Create: `src/DredgeAI.BidCompare.Domain.Shared/Evidences/EvidenceType.cs`
- Create: `src/DredgeAI.BidCompare.Domain.Shared/Evidences/EvidenceSeverity.cs`
- Create: `src/DredgeAI.BidCompare.Domain.Shared/Clauses/ClauseSource.cs`
- Create: `src/DredgeAI.BidCompare.Domain.Shared/Exports/ExportFormat.cs`
- Create: `src/DredgeAI.BidCompare.Domain.Shared/Exports/ExportJobStatus.cs`
- Create: `src/DredgeAI.BidCompare.Domain.Shared/BidCompareErrorCodes.cs`
- Test: `test/DredgeAI.BidCompare.Domain.Tests/BidCompareErrorCodesTests.cs`

**Steps:**

- [ ] **Step 1: 写失败测试（错误码命名空间唯一性）**

  创建 `test/DredgeAI.BidCompare.Domain.Tests/BidCompareErrorCodesTests.cs`：

  ```csharp
  using System.Linq;
  using System.Reflection;
  using Shouldly;
  using Xunit;

  namespace DredgeAI.BidCompare;

  public class BidCompareErrorCodesTests
  {
      [Fact]
      public void All_Error_Codes_Should_Start_With_Namespace_And_Be_Unique()
      {
          var values = typeof(BidCompareErrorCodes)
              .GetFields(BindingFlags.Public | BindingFlags.Static)
              .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name != nameof(BidCompareErrorCodes.Namespace))
              .Select(f => (string)f.GetRawConstantValue()!)
              .ToList();

          values.ShouldNotBeEmpty();
          values.ShouldAllBe(v => v.StartsWith(BidCompareErrorCodes.Namespace));
          values.Distinct().Count().ShouldBe(values.Count);
      }
  }
  ```

  ```bash
  dotnet test test/DredgeAI.BidCompare.Domain.Tests --filter BidCompareErrorCodesTests
  ```

  预期：**编译失败**（`BidCompareErrorCodes` 不存在），确认失败原因是类型缺失。

- [ ] **Step 2: 创建错误码常量**

  创建 `src/DredgeAI.BidCompare.Domain.Shared/BidCompareErrorCodes.cs`：

  ```csharp
  namespace DredgeAI.BidCompare;

  public static class BidCompareErrorCodes
  {
      public const string Namespace = "BidCompare:";

      public const string DocumentCountOutOfRange = Namespace + "DocumentCountOutOfRange";
      public const string UnsupportedFileType = Namespace + "UnsupportedFileType";
      public const string InvalidTaskState = Namespace + "InvalidTaskState";
      public const string DocumentNotFound = Namespace + "DocumentNotFound";
      public const string IrNotReady = Namespace + "IrNotReady";
      public const string IrValidationFailed = Namespace + "IrValidationFailed";
      public const string AnGineerParseFailed = Namespace + "AnGineerParseFailed";
      public const string NoTenderDocument = Namespace + "NoTenderDocument";
      public const string ClausesNotLocked = Namespace + "ClausesNotLocked";
      public const string ReportNotReady = Namespace + "ReportNotReady";
      public const string ExportJobNotFound = Namespace + "ExportJobNotFound";
      public const string ExportFailed = Namespace + "ExportFailed";
  }
  ```

- [ ] **Step 3: 创建全部枚举（整型序列化，取值与 spec §5/§6.1 语义一一对应）**

  `CompareTasks/CompareTaskStatus.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.CompareTasks;

  // spec §5: parsing → parsed → (待条款确认) → comparing → analyzing → done；异常态 failed/partial
  public enum CompareTaskStatus : byte
  {
      Parsing = 0,
      Parsed = 1,
      AwaitingClauses = 2,
      Comparing = 3,
      Analyzing = 4,
      Done = 5,
      Failed = 6,
      Partial = 7
  }
  ```

  `Documents/DocumentRole.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.Documents;

  // spec §6: 上传文档（标书/招标文件，区分 role）
  public enum DocumentRole : byte
  {
      Bid = 0,
      Tender = 1
  }
  ```

  `Documents/DocumentParseStatus.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.Documents;

  public enum DocumentParseStatus : byte
  {
      Pending = 0,
      Parsing = 1,
      Parsed = 2,
      Failed = 3
  }
  ```

  `Evidences/EvidenceType.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.Evidences;

  // spec §6.1: 'similarity'|'pricing'|'metadata'|'clause'|'indicator'
  public enum EvidenceType : byte
  {
      Similarity = 0,
      Pricing = 1,
      Metadata = 2,
      Clause = 3,
      Indicator = 4
  }
  ```

  `Evidences/EvidenceSeverity.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.Evidences;

  // spec §6.1: 'high'|'mid'|'low'
  public enum EvidenceSeverity : byte
  {
      High = 0,
      Mid = 1,
      Low = 2
  }
  ```

  `Clauses/ClauseSource.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.Clauses;

  // spec §6.1: 'extracted'|'manual'|'template'
  public enum ClauseSource : byte
  {
      Extracted = 0,
      Manual = 1,
      Template = 2
  }
  ```

  `Exports/ExportFormat.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.Exports;

  // spec §6: { format: 'pdf'|'word' }
  public enum ExportFormat : byte
  {
      Pdf = 0,
      Word = 1
  }
  ```

  `Exports/ExportJobStatus.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.Exports;

  public enum ExportJobStatus : byte
  {
      Pending = 0,
      Running = 1,
      Succeeded = 2,
      Failed = 3
  }
  ```

- [ ] **Step 4: 跑测试确认通过**

  ```bash
  dotnet test test/DredgeAI.BidCompare.Domain.Tests --filter BidCompareErrorCodesTests
  ```

  预期：1 passed。

- [ ] **Step 5: 提交**

  ```bash
  git add backend/DredgeAI.BidCompare
  git commit -m "feat(backend): add bid-compare enums and error codes in Domain.Shared"
  ```

---

## Task 3 【P1】Domain：CompareTask / CompareDocument 实体与状态机

**Files:**
- Create: `src/DredgeAI.BidCompare.Domain/CompareTasks/CompareTask.cs`
- Create: `src/DredgeAI.BidCompare.Domain/Documents/CompareDocument.cs`
- Test: `test/DredgeAI.BidCompare.Domain.Tests/CompareTasks/CompareTaskStateMachineTests.cs`

**Steps:**

- [ ] **Step 1: 写失败测试（状态机合法/非法流转，覆盖 spec §5 全部路径）**

  创建 `test/DredgeAI.BidCompare.Domain.Tests/CompareTasks/CompareTaskStateMachineTests.cs`：

  ```csharp
  using System;
  using DredgeAI.BidCompare.CompareTasks;
  using Shouldly;
  using Volo.Abp;
  using Xunit;

  namespace DredgeAI.BidCompare.CompareTasks;

  public class CompareTaskStateMachineTests
  {
      [Fact]
      public void New_Task_Should_Start_As_Parsing()
      {
          var task = new CompareTask(Guid.NewGuid(), "比标任务A");

          task.Status.ShouldBe(CompareTaskStatus.Parsing);
          task.ProgressStage.ShouldBe("parsing");
          task.ProgressPercent.ShouldBe(0);
      }

      [Fact]
      public void Name_Should_Be_Required()
      {
          Should.Throw<ArgumentException>(() => new CompareTask(Guid.NewGuid(), "  "));
      }

      [Fact]
      public void Happy_Path_With_Clause_Confirmation()
      {
          var task = new CompareTask(Guid.NewGuid(), "t");

          task.MarkParsed();
          task.Status.ShouldBe(CompareTaskStatus.Parsed);

          task.MarkAwaitingClauses();
          task.Status.ShouldBe(CompareTaskStatus.AwaitingClauses);

          task.LockClauseSnapshot("[{\"clauseId\":\"c1\"}]");
          task.MarkComparing();
          task.Status.ShouldBe(CompareTaskStatus.Comparing);

          task.MarkAnalyzing();
          task.Status.ShouldBe(CompareTaskStatus.Analyzing);

          task.MarkDone();
          task.Status.ShouldBe(CompareTaskStatus.Done);
      }

      [Fact]
      public void Partial_Should_Behave_Like_Parsed_For_Further_Transitions()
      {
          // spec §9: 单份解析失败 → 部分完成，其余文档照常对比
          var task = new CompareTask(Guid.NewGuid(), "t");

          task.MarkPartial("标书C.pdf: OCR 失败");
          task.Status.ShouldBe(CompareTaskStatus.Partial);
          task.FailureReason.ShouldContain("标书C.pdf");

          task.MarkAwaitingClauses();
          task.MarkComparing();
          task.MarkAnalyzing();
          task.MarkDone();
          task.Status.ShouldBe(CompareTaskStatus.Done);
      }

      [Fact]
      public void Failed_Should_Be_Terminal_From_Parsing_And_Comparing()
      {
          var task1 = new CompareTask(Guid.NewGuid(), "t");
          task1.MarkFailed("全部文档解析失败");
          task1.Status.ShouldBe(CompareTaskStatus.Failed);

          var task2 = new CompareTask(Guid.NewGuid(), "t");
          task2.MarkParsed();
          task2.MarkComparing();
          task2.MarkFailed("算法服务不可用");
          task2.Status.ShouldBe(CompareTaskStatus.Failed);
          task2.FailureReason.ShouldContain("算法服务不可用");
      }

      [Fact]
      public void Invalid_Transitions_Should_Throw_BusinessException()
      {
          var task = new CompareTask(Guid.NewGuid(), "t");

          Should.Throw<BusinessException>(() => task.MarkComparing())
              .Code.ShouldBe(BidCompareErrorCodes.InvalidTaskState);
          Should.Throw<BusinessException>(() => task.MarkDone())
              .Code.ShouldBe(BidCompareErrorCodes.InvalidTaskState);
          Should.Throw<BusinessException>(() => task.MarkAnalyzing())
              .Code.ShouldBe(BidCompareErrorCodes.InvalidTaskState);
      }

      [Fact]
      public void SetTenderDocument_Should_Only_Be_Set_During_Early_Stages()
      {
          var task = new CompareTask(Guid.NewGuid(), "t");
          var docId = Guid.NewGuid();

          task.SetTenderDocument(docId);
          task.TenderDocumentId.ShouldBe(docId);
      }

      [Fact]
      public void SetReport_Should_Require_Done()
      {
          var task = new CompareTask(Guid.NewGuid(), "t");
          Should.Throw<BusinessException>(() => task.SetReport("{}", DateTime.UtcNow))
              .Code.ShouldBe(BidCompareErrorCodes.InvalidTaskState);
      }
  }
  ```

  ```bash
  dotnet test test/DredgeAI.BidCompare.Domain.Tests --filter CompareTaskStateMachineTests
  ```

  预期：**编译失败**（`CompareTask` 不存在）。

- [ ] **Step 2: 实现 CompareTask 聚合根**

  创建 `src/DredgeAI.BidCompare.Domain/CompareTasks/CompareTask.cs`：

  ```csharp
  using System;
  using System.Linq;
  using Volo.Abp;
  using Volo.Abp.Domain.Entities.Auditing;

  namespace DredgeAI.BidCompare.CompareTasks;

  public class CompareTask : FullAuditedAggregateRoot<Guid>
  {
      public string Name { get; private set; } = default!;

      public CompareTaskStatus Status { get; private set; }

      public Guid? TenderDocumentId { get; private set; }

      /// <summary>条款清单快照（JSON 数组，元素见 ClauseSnapshotItem），锁定后不可变（spec §6.2）。</summary>
      public string? ClauseSnapshotJson { get; private set; }

      /// <summary>报告 JSON 缓存（CompareReportDto 序列化），任务 Done 后生成。</summary>
      public string? ReportJson { get; private set; }

      public DateTime? ReportGeneratedAt { get; private set; }

      public string ProgressStage { get; private set; } = "parsing";

      public int ProgressPercent { get; private set; }

      public string? ProgressMessage { get; private set; }

      /// <summary>Partial/Failed 的原因说明（spec §9 失败文档标注原因）。</summary>
      public string? FailureReason { get; private set; }

      protected CompareTask()
      {
      }

      public CompareTask(Guid id, string name) : base(id)
      {
          Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 128);
          Status = CompareTaskStatus.Parsing;
          ProgressStage = "parsing";
          ProgressPercent = 0;
      }

      public void SetTenderDocument(Guid documentId)
      {
          EnsureStatus(nameof(SetTenderDocument),
              CompareTaskStatus.Parsing, CompareTaskStatus.Parsed,
              CompareTaskStatus.Partial, CompareTaskStatus.AwaitingClauses);
          TenderDocumentId = documentId;
      }

      public void UpdateProgress(string stage, int percent, string? message = null)
      {
          ProgressStage = Check.NotNullOrWhiteSpace(stage, nameof(stage), maxLength: 32);
          ProgressPercent = Math.Clamp(percent, 0, 100);
          ProgressMessage = message;
      }

      public void MarkParsed()
      {
          EnsureStatus(nameof(MarkParsed),
              CompareTaskStatus.Parsing, CompareTaskStatus.Parsed,
              CompareTaskStatus.Partial, CompareTaskStatus.AwaitingClauses);
          Status = CompareTaskStatus.Parsed;
      }

      public void MarkPartial(string reason)
      {
          EnsureStatus(nameof(MarkPartial),
              CompareTaskStatus.Parsing, CompareTaskStatus.Parsed,
              CompareTaskStatus.Partial, CompareTaskStatus.AwaitingClauses);
          Status = CompareTaskStatus.Partial;
          FailureReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), maxLength: 2048);
      }

      public void MarkFailed(string reason)
      {
          EnsureStatus(nameof(MarkFailed),
              CompareTaskStatus.Parsing, CompareTaskStatus.Comparing, CompareTaskStatus.Analyzing);
          Status = CompareTaskStatus.Failed;
          FailureReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), maxLength: 2048);
      }

      public void MarkAwaitingClauses()
      {
          EnsureStatus(nameof(MarkAwaitingClauses), CompareTaskStatus.Parsed, CompareTaskStatus.Partial);
          Status = CompareTaskStatus.AwaitingClauses;
      }

      public void MarkComparing()
      {
          EnsureStatus(nameof(MarkComparing),
              CompareTaskStatus.Parsed, CompareTaskStatus.Partial, CompareTaskStatus.AwaitingClauses);
          Status = CompareTaskStatus.Comparing;
      }

      public void MarkAnalyzing()
      {
          EnsureStatus(nameof(MarkAnalyzing), CompareTaskStatus.Comparing);
          Status = CompareTaskStatus.Analyzing;
      }

      public void MarkDone()
      {
          EnsureStatus(nameof(MarkDone), CompareTaskStatus.Comparing, CompareTaskStatus.Analyzing);
          Status = CompareTaskStatus.Done;
      }

      public void LockClauseSnapshot(string snapshotJson)
      {
          EnsureStatus(nameof(LockClauseSnapshot),
              CompareTaskStatus.Parsing, CompareTaskStatus.Parsed,
              CompareTaskStatus.Partial, CompareTaskStatus.AwaitingClauses);
          ClauseSnapshotJson = Check.NotNullOrWhiteSpace(snapshotJson, nameof(snapshotJson));
      }

      public void SetReport(string reportJson, DateTime generatedAt)
      {
          EnsureStatus(nameof(SetReport), CompareTaskStatus.Done);
          ReportJson = Check.NotNullOrWhiteSpace(reportJson, nameof(reportJson));
          ReportGeneratedAt = generatedAt;
      }

      private void EnsureStatus(string action, params CompareTaskStatus[] allowed)
      {
          if (!allowed.Contains(Status))
          {
              throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                  .WithData("action", action)
                  .WithData("status", Status.ToString());
          }
      }
  }
  ```

- [ ] **Step 3: 实现 CompareDocument 实体**

  创建 `src/DredgeAI.BidCompare.Domain/Documents/CompareDocument.cs`：

  ```csharp
  using System;
  using Volo.Abp;
  using Volo.Abp.Domain.Entities.Auditing;

  namespace DredgeAI.BidCompare.Documents;

  public class CompareDocument : FullAuditedEntity<Guid>
  {
      public Guid TaskId { get; private set; }

      public DocumentRole Role { get; private set; }

      public string FileName { get; private set; } = default!;

      public string FileExtension { get; private set; } = default!;

      public long FileSize { get; private set; }

      /// <summary>原始文件对象存储 key：compare/{taskId}/{docId}/origin.{ext}。</summary>
      public string OriginStorageKey { get; private set; } = default!;

      public DocumentParseStatus ParseStatus { get; private set; }

      /// <summary>解析失败原因（spec §9 失败文档标注原因）。</summary>
      public string? ParseError { get; private set; }

      /// <summary>内部适配 IR 对象存储 key：compare/{taskId}/{docId}/ir.json（由 AnGIneer doc_blocks_graph 按 v2 映射生成，非跨系统交付物）。</summary>
      public string? IrStorageKey { get; private set; }

      /// <summary>content.md 对象存储 key：compare/{taskId}/{docId}/content.md（AnGIneer 阅读流 Markdown，LLM 语义层用）。</summary>
      public string? DocMdStorageKey { get; private set; }

      public int? PageCount { get; private set; }

      /// <summary>OCR 低置信（source=ocr 且 confidence&lt;0.5）块占比，spec §4.5 概览提示用；source/confidence 缺失（v2 降级期）时记 0。</summary>
      public double? OcrLowConfidenceRatio { get; private set; }

      protected CompareDocument()
      {
      }

      public CompareDocument(
          Guid id,
          Guid taskId,
          DocumentRole role,
          string fileName,
          long fileSize,
          string originStorageKey) : base(id)
      {
          TaskId = taskId;
          Role = role;
          FileName = Check.NotNullOrWhiteSpace(fileName, nameof(fileName), maxLength: 256);
          FileExtension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
          FileSize = fileSize;
          OriginStorageKey = Check.NotNullOrWhiteSpace(originStorageKey, nameof(originStorageKey), maxLength: 512);
          ParseStatus = DocumentParseStatus.Pending;
      }

      public void MarkParsing()
      {
          ParseStatus = DocumentParseStatus.Parsing;
          ParseError = null;
      }

      public void MarkParsed(string irStorageKey, string? docMdStorageKey, int pageCount, double ocrLowConfidenceRatio)
      {
          ParseStatus = DocumentParseStatus.Parsed;
          ParseError = null;
          IrStorageKey = Check.NotNullOrWhiteSpace(irStorageKey, nameof(irStorageKey), maxLength: 512);
          DocMdStorageKey = docMdStorageKey;
          PageCount = pageCount;
          OcrLowConfidenceRatio = ocrLowConfidenceRatio;
      }

      public void MarkParseFailed(string error)
      {
          ParseStatus = DocumentParseStatus.Failed;
          ParseError = Check.NotNullOrWhiteSpace(error, nameof(error), maxLength: 2048);
      }
  }
  ```

- [ ] **Step 4: 跑测试确认通过**

  ```bash
  dotnet test test/DredgeAI.BidCompare.Domain.Tests --filter CompareTaskStateMachineTests
  ```

  预期：7 passed。

- [ ] **Step 5: 提交**

  ```bash
  git add backend/DredgeAI.BidCompare
  git commit -m "feat(backend): add CompareTask aggregate with state machine and CompareDocument entity"
  ```

---

## Task 4 【P1】Domain：EvidenceItem / ClauseTemplate / ExportJob / ClauseSnapshotItem 实体

> P2 的实体（ClauseTemplate、ExportJob）提前在本任务创建，使 Task 5 只需一次 EF 迁移，避免 P1/P2 之间多次 schema 变更。

**Files:**
- Create: `src/DredgeAI.BidCompare.Domain/Evidences/EvidenceItem.cs`
- Create: `src/DredgeAI.BidCompare.Domain/Clauses/ClauseTemplate.cs`
- Create: `src/DredgeAI.BidCompare.Domain/Clauses/ClauseSnapshotItem.cs`
- Create: `src/DredgeAI.BidCompare.Domain/Exports/ExportJob.cs`
- Test: `test/DredgeAI.BidCompare.Domain.Tests/Exports/ExportJobTests.cs`

**Steps:**

- [ ] **Step 1: 写失败测试（ExportJob 生命周期 + EvidenceItem 构造）**

  创建 `test/DredgeAI.BidCompare.Domain.Tests/Exports/ExportJobTests.cs`：

  ```csharp
  using System;
  using DredgeAI.BidCompare.Evidences;
  using DredgeAI.BidCompare.Exports;
  using Shouldly;
  using Xunit;

  namespace DredgeAI.BidCompare.Exports;

  public class ExportJobTests
  {
      [Fact]
      public void ExportJob_Lifecycle()
      {
          var job = new ExportJob(Guid.NewGuid(), Guid.NewGuid(), ExportFormat.Pdf);
          job.Status.ShouldBe(ExportJobStatus.Pending);

          job.MarkRunning();
          job.Status.ShouldBe(ExportJobStatus.Running);

          job.MarkSucceeded("compare/t/exports/e.pdf");
          job.Status.ShouldBe(ExportJobStatus.Succeeded);
          job.FileStorageKey.ShouldBe("compare/t/exports/e.pdf");
      }

      [Fact]
      public void ExportJob_Can_Fail_With_Reason()
      {
          var job = new ExportJob(Guid.NewGuid(), Guid.NewGuid(), ExportFormat.Word);
          job.MarkRunning();
          job.MarkFailed("soffice 退出码 1");

          job.Status.ShouldBe(ExportJobStatus.Failed);
          job.Error.ShouldContain("soffice");
      }

      [Fact]
      public void EvidenceItem_Should_Keep_Payload()
      {
          var id = Guid.NewGuid();
          var taskId = Guid.NewGuid();
          var item = new EvidenceItem(
              id, taskId, EvidenceType.Similarity, EvidenceSeverity.High,
              docIdsJson: "[\"a\"]", locationsJson: "[]", metricsJson: "{\"similarity\":0.93}",
              title: "标书A与标书B大段雷同", description: "第3章相似度 0.93", aiGenerated: false);

          item.Id.ShouldBe(id);
          item.TaskId.ShouldBe(taskId);
          item.Type.ShouldBe(EvidenceType.Similarity);
          item.Severity.ShouldBe(EvidenceSeverity.High);
          item.AiGenerated.ShouldBeFalse();
          item.MetricsJson.ShouldContain("0.93");
      }
  }
  ```

  ```bash
  dotnet test test/DredgeAI.BidCompare.Domain.Tests --filter ExportJobTests
  ```

  预期：**编译失败**（类型不存在）。

- [ ] **Step 2: 实现 EvidenceItem（spec §6.1 Evidence 逐字段对应，JSON 负载 text 存储）**

  创建 `src/DredgeAI.BidCompare.Domain/Evidences/EvidenceItem.cs`：

  ```csharp
  using System;
  using Volo.Abp;
  using Volo.Abp.Domain.Entities.Auditing;

  namespace DredgeAI.BidCompare.Evidences;

  /// <summary>
  /// 证据项（spec §3.2 核心数据结构）。DocIds/Locations/Metrics 以 JSON text 列存储，
  /// 结构与 spec §6.1 一致：docIds[]、locations: { docId, blockIds[] }[]、metrics: { similarity? }。
  /// </summary>
  public class EvidenceItem : FullAuditedEntity<Guid>
  {
      public Guid TaskId { get; private set; }

      public EvidenceType Type { get; private set; }

      public EvidenceSeverity Severity { get; private set; }

      public string DocIdsJson { get; private set; } = "[]";

      public string LocationsJson { get; private set; } = "[]";

      public string? MetricsJson { get; private set; }

      public string Title { get; private set; } = default!;

      public string Description { get; private set; } = default!;

      /// <summary>spec §3.2: 算法证据与 AI 结论在 UI 上可区分。</summary>
      public bool AiGenerated { get; private set; }

      protected EvidenceItem()
      {
      }

      public EvidenceItem(
          Guid id,
          Guid taskId,
          EvidenceType type,
          EvidenceSeverity severity,
          string docIdsJson,
          string locationsJson,
          string? metricsJson,
          string title,
          string description,
          bool aiGenerated) : base(id)
      {
          TaskId = taskId;
          Type = type;
          Severity = severity;
          DocIdsJson = Check.NotNullOrWhiteSpace(docIdsJson, nameof(docIdsJson));
          LocationsJson = Check.NotNullOrWhiteSpace(locationsJson, nameof(locationsJson));
          MetricsJson = metricsJson;
          Title = Check.NotNullOrWhiteSpace(title, nameof(title), maxLength: 512);
          Description = Check.NotNullOrWhiteSpace(description, nameof(description), maxLength: 4000);
          AiGenerated = aiGenerated;
      }
  }
  ```

- [ ] **Step 3: 实现 ClauseTemplate / ClauseSnapshotItem / ExportJob**

  `src/DredgeAI.BidCompare.Domain/Clauses/ClauseTemplate.cs`：

  ```csharp
  using System;
  using Volo.Abp;
  using Volo.Abp.Domain.Entities.Auditing;

  namespace DredgeAI.BidCompare.Clauses;

  /// <summary>个人条款库模板（spec §1 条款来源之一：用户手动维护）。</summary>
  public class ClauseTemplate : FullAuditedAggregateRoot<Guid>
  {
      public string Text { get; private set; } = default!;

      public bool Mandatory { get; private set; }

      public string? Category { get; private set; }

      protected ClauseTemplate()
      {
      }

      public ClauseTemplate(Guid id, string text, bool mandatory, string? category) : base(id)
      {
          SetValues(text, mandatory, category);
      }

      public void Update(string text, bool mandatory, string? category)
      {
          SetValues(text, mandatory, category);
      }

      private void SetValues(string text, bool mandatory, string? category)
      {
          Text = Check.NotNullOrWhiteSpace(text, nameof(text), maxLength: 2000);
          Mandatory = mandatory;
          Category = category == null ? null : Check.NotNullOrWhiteSpace(category, nameof(category), maxLength: 64);
      }
  }
  ```

  `src/DredgeAI.BidCompare.Domain/Clauses/ClauseSnapshotItem.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.Clauses;

  /// <summary>
  /// 任务内条款快照元素（spec §6.1 Clause，序列化进 CompareTask.ClauseSnapshotJson）。
  /// JSON 字段名 camelCase：clauseId/source/text/mandatory/category。
  /// </summary>
  public class ClauseSnapshotItem
  {
      public string ClauseId { get; set; } = default!;

      public ClauseSource Source { get; set; }

      public string Text { get; set; } = default!;

      public bool Mandatory { get; set; }

      public string? Category { get; set; }
  }
  ```

  `src/DredgeAI.BidCompare.Domain/Exports/ExportJob.cs`：

  ```csharp
  using System;
  using Volo.Abp;
  using Volo.Abp.Domain.Entities.Auditing;

  namespace DredgeAI.BidCompare.Exports;

  /// <summary>导出任务句柄（spec §6.2 导出异步化）。</summary>
  public class ExportJob : FullAuditedEntity<Guid>
  {
      public Guid TaskId { get; private set; }

      public ExportFormat Format { get; private set; }

      public ExportJobStatus Status { get; private set; }

      /// <summary>导出文件对象存储 key：compare/{taskId}/exports/{jobId}.{pdf|docx}。</summary>
      public string? FileStorageKey { get; private set; }

      /// <summary>失败原因（spec §9 导出失败可重试）。</summary>
      public string? Error { get; private set; }

      protected ExportJob()
      {
      }

      public ExportJob(Guid id, Guid taskId, ExportFormat format) : base(id)
      {
          TaskId = taskId;
          Format = format;
          Status = ExportJobStatus.Pending;
      }

      public void MarkRunning()
      {
          Status = ExportJobStatus.Running;
          Error = null;
      }

      public void MarkSucceeded(string fileStorageKey)
      {
          Status = ExportJobStatus.Succeeded;
          FileStorageKey = Check.NotNullOrWhiteSpace(fileStorageKey, nameof(fileStorageKey), maxLength: 512);
          Error = null;
      }

      public void MarkFailed(string error)
      {
          Status = ExportJobStatus.Failed;
          Error = Check.NotNullOrWhiteSpace(error, nameof(error), maxLength: 2048);
      }
  }
  ```

- [ ] **Step 4: 跑测试确认通过**

  ```bash
  dotnet test test/DredgeAI.BidCompare.Domain.Tests --filter ExportJobTests
  ```

  预期：3 passed。

- [ ] **Step 5: 提交**

  ```bash
  git add backend/DredgeAI.BidCompare
  git commit -m "feat(backend): add EvidenceItem, ClauseTemplate, ClauseSnapshotItem, ExportJob entities"
  ```

---

## Task 5 【P1】EntityFrameworkCore：DbContext、实体映射、初始迁移

**Files:**
- Modify: `src/DredgeAI.BidCompare.EntityFrameworkCore/EntityFrameworkCore/BidCompareDbContext.cs`
- Create: `src/DredgeAI.BidCompare.EntityFrameworkCore/Migrations/`（`dotnet ef` 生成）
- Test: `test/DredgeAI.BidCompare.EntityFrameworkCore.Tests/EntityFrameworkCore/BidCompareDbContextTests.cs`

**Steps:**

- [ ] **Step 1: 写失败测试（DbContext 含全部 DbSet，可插入查询）**

  创建 `test/DredgeAI.BidCompare.EntityFrameworkCore.Tests/EntityFrameworkCore/BidCompareDbContextTests.cs`：

  ```csharp
  using System;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.Clauses;
  using DredgeAI.BidCompare.CompareTasks;
  using DredgeAI.BidCompare.Documents;
  using DredgeAI.BidCompare.Evidences;
  using DredgeAI.BidCompare.Exports;
  using Shouldly;
  using Volo.Abp.Domain.Repositories;
  using Xunit;

  namespace DredgeAI.BidCompare.EntityFrameworkCore;

  public class BidCompareDbContextTests : BidCompareEntityFrameworkCoreTestBase
  {
      [Fact]
      public async Task Should_Persist_All_BidCompare_Aggregates()
      {
          var taskId = Guid.NewGuid();
          var taskRepo = ServiceProvider.GetRequiredService<IRepository<CompareTask, Guid>>();
          var docRepo = ServiceProvider.GetRequiredService<IRepository<CompareDocument, Guid>>();
          var evidenceRepo = ServiceProvider.GetRequiredService<IRepository<EvidenceItem, Guid>>();
          var templateRepo = ServiceProvider.GetRequiredService<IRepository<ClauseTemplate, Guid>>();
          var exportRepo = ServiceProvider.GetRequiredService<IRepository<ExportJob, Guid>>();

          await WithUnitOfWorkAsync(async () =>
          {
              await taskRepo.InsertAsync(new CompareTask(taskId, "任务1"));
              await docRepo.InsertAsync(new CompareDocument(Guid.NewGuid(), taskId,
                  DocumentRole.Bid, "标书A.pdf", 1024, "compare/t/d/origin.pdf"));
              await evidenceRepo.InsertAsync(new EvidenceItem(Guid.NewGuid(), taskId,
                  EvidenceType.Similarity, EvidenceSeverity.High, "[]", "[]", null, "t", "d", false));
              await templateRepo.InsertAsync(new ClauseTemplate(Guid.NewGuid(), "须提供资质证书", true, "资质"));
              await exportRepo.InsertAsync(new ExportJob(Guid.NewGuid(), taskId, ExportFormat.Pdf));
          });

          (await taskRepo.GetCountAsync()).ShouldBe(1);
          (await docRepo.GetCountAsync()).ShouldBe(1);
          (await evidenceRepo.GetCountAsync()).ShouldBe(1);
          (await templateRepo.GetCountAsync()).ShouldBe(1);
          (await exportRepo.GetCountAsync()).ShouldBe(1);
      }
  }
  ```

  > 注：`ServiceProvider`、`WithUnitOfWorkAsync` 来自模板自带的 `BidCompareEntityFrameworkCoreTestBase`（其 test module 用 SQLite in-memory + 迁移建库）。

  ```bash
  dotnet test test/DredgeAI.BidCompare.EntityFrameworkCore.Tests --filter BidCompareDbContextTests
  ```

  预期：**失败**（DbSet 未配置，实体未映射）。

- [ ] **Step 2: 配置 DbContext 与实体映射**

  将 `src/DredgeAI.BidCompare.EntityFrameworkCore/EntityFrameworkCore/BidCompareDbContext.cs` 全文替换为（保留模板原有 `using` 与基类，仅展示最终完整文件）：

  ```csharp
  using DredgeAI.BidCompare.Clauses;
  using DredgeAI.BidCompare.CompareTasks;
  using DredgeAI.BidCompare.Documents;
  using DredgeAI.BidCompare.Evidences;
  using DredgeAI.BidCompare.Exports;
  using Microsoft.EntityFrameworkCore;
  using Volo.Abp.AuditLogging.EntityFrameworkCore;
  using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
  using Volo.Abp.Data;
  using Volo.Abp.EntityFrameworkCore;
  using Volo.Abp.FeatureManagement.EntityFrameworkCore;
  using Volo.Abp.Identity.EntityFrameworkCore;
  using Volo.Abp.OpenIddict.EntityFrameworkCore;
  using Volo.Abp.PermissionManagement.EntityFrameworkCore;
  using Volo.Abp.SettingManagement.EntityFrameworkCore;
  using Volo.Abp.TenantManagement.EntityFrameworkCore;

  namespace DredgeAI.BidCompare.EntityFrameworkCore;

  [ConnectionStringName("Default")]
  public class BidCompareDbContext : AbpDbContext<BidCompareDbContext>
  {
      public DbSet<CompareTask> CompareTasks { get; set; }
      public DbSet<CompareDocument> CompareDocuments { get; set; }
      public DbSet<EvidenceItem> EvidenceItems { get; set; }
      public DbSet<ClauseTemplate> ClauseTemplates { get; set; }
      public DbSet<ExportJob> ExportJobs { get; set; }

      public BidCompareDbContext(DbContextOptions<BidCompareDbContext> options)
          : base(options)
      {
      }

      protected override void OnModelCreating(ModelBuilder builder)
      {
          base.OnModelCreating(builder);

          /* ABP 模块表（模板原有配置，勿删） */
          builder.ConfigurePermissionManagement();
          builder.ConfigureSettingManagement();
          builder.ConfigureBackgroundJobs();
          builder.ConfigureAuditLogging();
          builder.ConfigureIdentity();
          builder.ConfigureOpenIddict();
          builder.ConfigureFeatureManagement();
          builder.ConfigureTenantManagement();

          builder.Entity<CompareTask>(b =>
          {
              b.ToTable("BcCompareTasks");
              b.ConfigureByConvention();
              b.Property(x => x.Name).IsRequired().HasMaxLength(128);
              b.Property(x => x.Status).IsRequired();
              b.Property(x => x.ClauseSnapshotJson).HasColumnType("text");
              b.Property(x => x.ReportJson).HasColumnType("text");
              b.Property(x => x.ProgressStage).IsRequired().HasMaxLength(32);
              b.Property(x => x.ProgressMessage).HasMaxLength(1024);
              b.Property(x => x.FailureReason).HasMaxLength(2048);
              b.HasIndex(x => x.Status);
          });

          builder.Entity<CompareDocument>(b =>
          {
              b.ToTable("BcCompareDocuments");
              b.ConfigureByConvention();
              b.Property(x => x.FileName).IsRequired().HasMaxLength(256);
              b.Property(x => x.FileExtension).IsRequired().HasMaxLength(16);
              b.Property(x => x.OriginStorageKey).IsRequired().HasMaxLength(512);
              b.Property(x => x.IrStorageKey).HasMaxLength(512);
              b.Property(x => x.DocMdStorageKey).HasMaxLength(512);
              b.Property(x => x.ParseError).HasMaxLength(2048);
              b.HasIndex(x => x.TaskId);
          });

          builder.Entity<EvidenceItem>(b =>
          {
              b.ToTable("BcEvidenceItems");
              b.ConfigureByConvention();
              b.Property(x => x.DocIdsJson).IsRequired().HasColumnType("text");
              b.Property(x => x.LocationsJson).IsRequired().HasColumnType("text");
              b.Property(x => x.MetricsJson).HasColumnType("text");
              b.Property(x => x.Title).IsRequired().HasMaxLength(512);
              b.Property(x => x.Description).IsRequired().HasMaxLength(4000);
              b.HasIndex(x => new { x.TaskId, x.Type });
              b.HasIndex(x => new { x.TaskId, x.Severity });
          });

          builder.Entity<ClauseTemplate>(b =>
          {
              b.ToTable("BcClauseTemplates");
              b.ConfigureByConvention();
              b.Property(x => x.Text).IsRequired().HasMaxLength(2000);
              b.Property(x => x.Category).HasMaxLength(64);
          });

          builder.Entity<ExportJob>(b =>
          {
              b.ToTable("BcExportJobs");
              b.ConfigureByConvention();
              b.Property(x => x.FileStorageKey).HasMaxLength(512);
              b.Property(x => x.Error).HasMaxLength(2048);
              b.HasIndex(x => x.TaskId);
          });
      }
  }
  ```

  > 注：若模板未引用某些模块（如无 TenantManagement），以模板生成的 `using`/`ConfigureXxx()` 清单为准保留原样，仅追加 5 个业务实体的 `builder.Entity<>` 配置与 DbSet。

- [ ] **Step 3: 生成并应用初始迁移**

  ```bash
  cd backend/DredgeAI.BidCompare
  dotnet ef migrations add Initial_BidCompare \
    -p src/DredgeAI.BidCompare.EntityFrameworkCore \
    -s src/DredgeAI.BidCompare.HttpApi.Host
  dotnet ef database update \
    -p src/DredgeAI.BidCompare.EntityFrameworkCore \
    -s src/DredgeAI.BidCompare.HttpApi.Host
  ```

  预期：迁移生成含 `BcCompareTasks / BcCompareDocuments / BcEvidenceItems / BcClauseTemplates / BcExportJobs` 五张表；数据库更新成功。

- [ ] **Step 4: 跑测试确认通过**

  ```bash
  dotnet test test/DredgeAI.BidCompare.EntityFrameworkCore.Tests --filter BidCompareDbContextTests
  ```

  预期：1 passed（SQLite 下 `HasColumnType("text")` 兼容）。

- [ ] **Step 5: 提交**

  ```bash
  git add backend/DredgeAI.BidCompare
  git commit -m "feat(backend): configure DbContext mappings and add Initial_BidCompare migration"
  ```

---

## Task 6 【P1】对象存储：IFileStorage + S3(MinIO) 实现 + 测试 Fake

**Files:**
- Create: `src/DredgeAI.BidCompare.Domain/Storage/IFileStorage.cs`
- Create: `src/DredgeAI.BidCompare.HttpApi.Host/Storage/S3StorageOptions.cs`
- Create: `src/DredgeAI.BidCompare.HttpApi.Host/Storage/S3FileStorage.cs`
- Modify: `src/DredgeAI.BidCompare.HttpApi.Host/BidCompareHttpApiHostModule.cs`
- Modify: `src/DredgeAI.BidCompare.HttpApi.Host/appsettings.json`
- Modify: `src/DredgeAI.BidCompare.HttpApi.Host/DredgeAI.BidCompare.HttpApi.Host.csproj`（加 AWSSDK.S3）
- Test: `test/DredgeAI.BidCompare.TestBase/Fakes/InMemoryFileStorage.cs`
- Test: `test/DredgeAI.BidCompare.TestBase/Fakes/RecordingBackgroundJobManager.cs`
- Test: `test/DredgeAI.BidCompare.Domain.Tests/Storage/InMemoryFileStorageTests.cs`

**Steps:**

- [ ] **Step 1: 写失败测试（InMemoryFileStorage 行为契约，S3 实现须满足同一契约）**

  创建 `test/DredgeAI.BidCompare.Domain.Tests/Storage/InMemoryFileStorageTests.cs`：

  ```csharp
  using System.IO;
  using System.Text;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.Storage;
  using Shouldly;
  using Xunit;

  namespace DredgeAI.BidCompare.Storage;

  public class InMemoryFileStorageTests
  {
      [Fact]
      public async Task Upload_Get_Exists_Delete_Roundtrip()
      {
          IFileStorage storage = new InMemoryFileStorage();
          var bytes = Encoding.UTF8.GetBytes("hello");

          (await storage.ExistsAsync("k1")).ShouldBeFalse();

          await storage.UploadAsync("k1", new MemoryStream(bytes), "text/plain");
          (await storage.ExistsAsync("k1")).ShouldBeTrue();

          await using var stream = await storage.GetAsync("k1");
          using var reader = new StreamReader(stream);
          (await reader.ReadToEndAsync()).ShouldBe("hello");

          var url = await storage.GetPresignedUrlAsync("k1", System.TimeSpan.FromHours(1));
          url.ShouldNotBeNullOrWhiteSpace();

          await storage.DeleteAsync("k1");
          (await storage.ExistsAsync("k1")).ShouldBeFalse();
      }
  }
  ```

  ```bash
  dotnet test test/DredgeAI.BidCompare.Domain.Tests --filter InMemoryFileStorageTests
  ```

  预期：**编译失败**（`IFileStorage`/`InMemoryFileStorage` 不存在）。

- [ ] **Step 2: 定义 IFileStorage 接口**

  创建 `src/DredgeAI.BidCompare.Domain/Storage/IFileStorage.cs`：

  ```csharp
  using System;
  using System.IO;
  using System.Threading;
  using System.Threading.Tasks;

  namespace DredgeAI.BidCompare.Storage;

  /// <summary>
  /// 对象存储抽象（原始文件 / IR 包 / 导出文件）。
  /// 生产实现：S3 兼容（MinIO）AWSSDK.S3；测试实现：InMemoryFileStorage。
  /// key 约定：compare/{taskId}/{docId}/origin.{ext}、compare/{taskId}/{docId}/ir.json（内部适配 IR）、
  /// compare/{taskId}/{docId}/content.md、compare/{taskId}/{docId}/images/...、
  /// compare/{taskId}/{docId}/raw/（AnGIneer 原始产物留档）、compare/{taskId}/exports/{jobId}.{ext}
  /// </summary>
  public interface IFileStorage
  {
      /// <summary>上传对象，返回存储 key。</summary>
      Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

      /// <summary>读取对象内容。调用方负责 Dispose 返回的流。</summary>
      Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default);

      Task DeleteAsync(string key, CancellationToken cancellationToken = default);

      Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

      /// <summary>生成限时下载链接（导出文件下载用，spec §6.2）。</summary>
      Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
  }
  ```

- [ ] **Step 3: 实现 InMemoryFileStorage + RecordingBackgroundJobManager（TestBase Fakes）**

  创建 `test/DredgeAI.BidCompare.TestBase/Fakes/InMemoryFileStorage.cs`：

  ```csharp
  using System;
  using System.Collections.Concurrent;
  using System.IO;
  using System.Threading;
  using System.Threading.Tasks;

  namespace DredgeAI.BidCompare.Storage;

  /// <summary>IFileStorage 内存实现，供全部测试工程使用。</summary>
  public class InMemoryFileStorage : IFileStorage
  {
      public ConcurrentDictionary<string, byte[]> Objects { get; } = new();

      public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
      {
          using var buffer = new MemoryStream();
          content.CopyTo(buffer);
          Objects[key] = buffer.ToArray();
          return Task.FromResult(key);
      }

      public Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
      {
          if (!Objects.TryGetValue(key, out var bytes))
          {
              throw new FileNotFoundException($"Object not found: {key}", key);
          }
          return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
      }

      public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
      {
          Objects.TryRemove(key, out _);
          return Task.CompletedTask;
      }

      public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
      {
          return Task.FromResult(Objects.ContainsKey(key));
      }

      public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
      {
          return Task.FromResult($"memory://{key}?expiry={expiry.TotalMinutes}m");
      }
  }
  ```

  创建 `test/DredgeAI.BidCompare.TestBase/Fakes/RecordingBackgroundJobManager.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Threading.Tasks;
  using Volo.Abp.BackgroundJobs;

  namespace DredgeAI.BidCompare;

  /// <summary>记录入队参数而不真正执行；测试手动解析 Job 类并调用 ExecuteAsync，保证确定性。</summary>
  public class RecordingBackgroundJobManager : IBackgroundJobManager
  {
      public List<object> EnqueuedArgs { get; } = new();

      public Task<string> EnqueueAsync<TArgs>(
          TArgs args,
          BackgroundJobPriority priority = BackgroundJobPriority.Normal,
          TimeSpan? delay = null)
      {
          EnqueuedArgs.Add(args!);
          return Task.FromResult(Guid.NewGuid().ToString("N"));
      }

      public void Clear() => EnqueuedArgs.Clear();

      public TArgs? LastEnqueued<TArgs>()
      {
          for (var i = EnqueuedArgs.Count - 1; i >= 0; i--)
          {
              if (EnqueuedArgs[i] is TArgs typed)
              {
                  return typed;
              }
          }
          return default;
      }
  }
  ```

  > 注：TestBase 工程需引用 `DredgeAI.BidCompare.Domain`（模板默认已引用）。Fakes 放在 TestBase 使 Domain.Tests / Application.Tests 均可复用。

- [ ] **Step 4: 跑测试确认通过**

  ```bash
  dotnet test test/DredgeAI.BidCompare.Domain.Tests --filter InMemoryFileStorageTests
  ```

  预期：1 passed。

- [ ] **Step 5: 实现 S3FileStorage（AWSSDK.S3，生产实现）**

  ```bash
  cd backend/DredgeAI.BidCompare
  dotnet add src/DredgeAI.BidCompare.HttpApi.Host package AWSSDK.S3
  ```

  创建 `src/DredgeAI.BidCompare.HttpApi.Host/Storage/S3StorageOptions.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.Storage;

  public class S3StorageOptions
  {
      /// <summary>MinIO 服务地址，如 http://localhost:9000。</summary>
      public string ServiceUrl { get; set; } = "http://localhost:9000";

      public string AccessKey { get; set; } = "minioadmin";

      public string SecretKey { get; set; } = "minioadmin";

      public string Bucket { get; set; } = "bid-compare";

      /// <summary>MinIO 需要 path-style 寻址。</summary>
      public bool ForcePathStyle { get; set; } = true;
  }
  ```

  创建 `src/DredgeAI.BidCompare.HttpApi.Host/Storage/S3FileStorage.cs`：

  ```csharp
  using System;
  using System.IO;
  using System.Net;
  using System.Threading;
  using System.Threading.Tasks;
  using Amazon.Runtime;
  using Amazon.S3;
  using Amazon.S3.Model;
  using Microsoft.Extensions.Options;
  using Volo.Abp.DependencyInjection;

  namespace DredgeAI.BidCompare.Storage;

  /// <summary>S3 兼容对象存储实现（MinIO），统一使用 AWSSDK.S3。</summary>
  public class S3FileStorage : IFileStorage, ISingletonDependency
  {
      private readonly S3StorageOptions _options;
      private readonly Lazy<IAmazonS3> _client;

      public S3FileStorage(IOptions<S3StorageOptions> options)
      {
          _options = options.Value;
          _client = new Lazy<IAmazonS3>(() => new AmazonS3Client(
              new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
              new AmazonS3Config
              {
                  ServiceURL = _options.ServiceUrl,
                  ForcePathStyle = _options.ForcePathStyle
              }));
      }

      public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
      {
          await EnsureBucketAsync(cancellationToken);
          var request = new PutObjectRequest
          {
              BucketName = _options.Bucket,
              Key = key,
              InputStream = content,
              ContentType = contentType
          };
          await _client.Value.PutObjectAsync(request, cancellationToken);
          return key;
      }

      public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
      {
          var response = await _client.Value.GetObjectAsync(_options.Bucket, key, cancellationToken);
          var buffer = new MemoryStream();
          await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
          buffer.Position = 0;
          response.Dispose();
          return buffer;
      }

      public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
      {
          await _client.Value.DeleteObjectAsync(_options.Bucket, key, cancellationToken);
      }

      public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
      {
          try
          {
              await _client.Value.GetObjectMetadataAsync(_options.Bucket, key, cancellationToken);
              return true;
          }
          catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
          {
              return false;
          }
      }

      public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
      {
          var request = new GetPreSignedUrlRequest
          {
              BucketName = _options.Bucket,
              Key = key,
              Expires = DateTime.UtcNow.Add(expiry),
              Verb = HttpVerb.GET
          };
          return Task.FromResult(_client.Value.GetPreSignedURL(request));
      }

      private async Task EnsureBucketAsync(CancellationToken cancellationToken)
      {
          try
          {
              await _client.Value.PutBucketAsync(new PutBucketRequest { BucketName = _options.Bucket }, cancellationToken);
          }
          catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.Conflict)
          {
              // BucketAlreadyOwnedByYou：桶已存在，忽略
          }
      }
  }
  ```

- [ ] **Step 6: 注册 Options 并补配置**

  在 `BidCompareHttpApiHostModule.ConfigureServices` 末尾追加：

  ```csharp
  Configure<S3StorageOptions>(context.Configuration.GetSection("Storage:S3"));
  ```

  在 `appsettings.json` 顶层追加：

  ```json
  "Storage": {
    "S3": {
      "ServiceUrl": "http://localhost:9000",
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin",
      "Bucket": "bid-compare",
      "ForcePathStyle": true
    }
  }
  ```

  > `S3FileStorage` 标了 `ISingletonDependency`，ABP 自动注册为 `IFileStorage` 实现（默认约定注册接口），无需显式 Add。

- [ ] **Step 7: 编译验证并提交**

  ```bash
  dotnet build DredgeAI.BidCompare.sln
  ```

  预期：0 error（S3FileStorage 不在单元测试覆盖范围，MinIO 连通性在 Task 15 联调项中人工验证）。

  ```bash
  git add backend/DredgeAI.BidCompare
  git commit -m "feat(backend): add IFileStorage abstraction with S3 (MinIO) implementation and in-memory fake"
  ```

---

## Task 7 【P1】任务与文档管理 API（创建/详情/列表/删除/上传文档）

覆盖 spec §6 路由：`POST /api/compare/tasks`、`GET /api/compare/tasks/{id}`、`GET /api/compare/tasks`、`POST /api/compare/tasks/{id}/documents`；补充路由 `DELETE /api/compare/tasks/{id}`。

**Files:**
- Create: `src/DredgeAI.BidCompare.Application.Contracts/CompareTasks/CompareTaskDto.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/CompareTasks/CompareProgressDto.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/CompareTasks/CreateCompareTaskDto.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/CompareTasks/GetCompareTasksInput.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/CompareTasks/ICompareTaskAppService.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/Documents/CompareDocumentDto.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/Clauses/ClauseDto.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/Clauses/ClauseInputDto.cs`
- Create: `src/DredgeAI.BidCompare.Application/CompareTasks/CompareTaskAppService.cs`
- Create: `src/DredgeAI.BidCompare.Application/BackgroundJobs/ParseDocumentArgs.cs`
- Create: `src/DredgeAI.BidCompare.HttpApi/Controllers/CompareTaskController.cs`
- Modify: `test/DredgeAI.BidCompare.Application.Tests/BidCompareApplicationTestModule.cs`
- Test: `test/DredgeAI.BidCompare.Application.Tests/CompareTasks/CompareTaskAppServiceTests.cs`

**Steps:**

- [ ] **Step 1: 写失败测试（创建/列表/详情/上传校验/删除）**

  创建 `test/DredgeAI.BidCompare.Application.Tests/CompareTasks/CompareTaskAppServiceTests.cs`：

  ```csharp
  using System;
  using System.IO;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.CompareTasks;
  using DredgeAI.BidCompare.Documents;
  using DredgeAI.BidCompare.Storage;
  using Shouldly;
  using Volo.Abp;
  using Volo.Abp.Domain.Repositories;
  using Xunit;

  namespace DredgeAI.BidCompare.CompareTasks;

  public class CompareTaskAppServiceTests : BidCompareApplicationTestBase
  {
      private readonly ICompareTaskAppService _appService;
      private readonly InMemoryFileStorage _fileStorage;
      private readonly RecordingBackgroundJobManager _jobManager;

      public CompareTaskAppServiceTests()
      {
          _appService = GetRequiredService<ICompareTaskAppService>();
          _fileStorage = (InMemoryFileStorage)GetRequiredService<IFileStorage>();
          _jobManager = (RecordingBackgroundJobManager)GetRequiredService<Volo.Abp.BackgroundJobs.IBackgroundJobManager>();
      }

      [Fact]
      public async Task Create_Then_Get_Should_Return_Spec_Fields()
      {
          var created = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "一期工程比标" });

          created.Id.ShouldNotBe(Guid.Empty);
          created.Name.ShouldBe("一期工程比标");
          created.Status.ShouldBe(CompareTaskStatus.Parsing);
          created.DocIds.ShouldBeEmpty();
          created.TenderDocId.ShouldBeNull();
          created.ClauseSnapshot.ShouldBeNull();
          created.Progress.Stage.ShouldBe("parsing");
          created.CreatedAt.ShouldBeGreaterThan(DateTime.MinValue);

          var fetched = await _appService.GetAsync(created.Id);
          fetched.Name.ShouldBe("一期工程比标");
      }

      [Fact]
      public async Task Create_With_Clauses_Should_Lock_Snapshot()
      {
          var created = await _appService.CreateAsync(new CreateCompareTaskDto
          {
              Name = "t",
              Clauses = new()
              {
                  new ClauseInputDto { Text = "须提供 ISO9001 证书", Mandatory = true, Category = "资质" }
              }
          });

          created.ClauseSnapshot.ShouldNotBeNull();
          created.ClauseSnapshot!.Count.ShouldBe(1);
          created.ClauseSnapshot[0].ClauseId.ShouldNotBeNullOrWhiteSpace();
          created.ClauseSnapshot[0].Source.ShouldBe(Clauses.ClauseSource.Manual);
          created.ClauseSnapshot[0].Mandatory.ShouldBeTrue();
      }

      [Fact]
      public async Task GetList_Should_Page_And_Filter()
      {
          await _appService.CreateAsync(new CreateCompareTaskDto { Name = "道路项目" });
          await _appService.CreateAsync(new CreateCompareTaskDto { Name = "桥梁项目" });

          var all = await _appService.GetListAsync(new GetCompareTasksInput { MaxResultCount = 10 });
          all.TotalCount.ShouldBe(2);
          all.Items.Count.ShouldBe(2);

          var filtered = await _appService.GetListAsync(new GetCompareTasksInput { Name = "道路", MaxResultCount = 10 });
          filtered.TotalCount.ShouldBe(1);
          filtered.Items[0].Name.ShouldBe("道路项目");
      }

      [Fact]
      public async Task UploadDocument_Should_Store_File_And_Enqueue_Parse()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
          var content = new MemoryStream(Encoding.UTF8.GetBytes("%PDF fake"));

          var doc = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", content);

          doc.TaskId.ShouldBe(task.Id);
          doc.Role.ShouldBe(DocumentRole.Bid);
          doc.ParseStatus.ShouldBe(DocumentParseStatus.Pending);
          _fileStorage.Objects.Keys.ShouldContain(k => k.StartsWith($"compare/{task.Id}/{doc.Id}/origin"));

          var enqueued = _jobManager.LastEnqueued<ParseDocumentArgs>();
          enqueued.ShouldNotBeNull();
          enqueued!.TaskId.ShouldBe(task.Id);
          enqueued.DocumentId.ShouldBe(doc.Id);

          var detail = await _appService.GetAsync(task.Id);
          detail.DocIds.ShouldContain(doc.Id);
      }

      [Fact]
      public async Task UploadDocument_Should_Reject_Unsupported_Extension()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });

          var ex = await Should.ThrowAsync<BusinessException>(() =>
              _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "名单.xlsx",
                  new MemoryStream(new byte[] { 1 })));
          ex.Code.ShouldBe(BidCompareErrorCodes.UnsupportedFileType);
      }

      [Fact]
      public async Task UploadDocument_Should_Enforce_Max_5_Bid_Documents()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
          for (var i = 0; i < 5; i++)
          {
              await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, $"标书{i}.pdf",
                  new MemoryStream(new byte[] { 1 }));
          }

          var ex = await Should.ThrowAsync<BusinessException>(() =>
              _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "第6份.pdf",
                  new MemoryStream(new byte[] { 1 })));
          ex.Code.ShouldBe(BidCompareErrorCodes.DocumentCountOutOfRange);
      }

      [Fact]
      public async Task Upload_Tender_Document_Should_Set_TenderDocId()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
          var doc = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Tender, "招标文件.pdf",
              new MemoryStream(new byte[] { 1 }));

          var detail = await _appService.GetAsync(task.Id);
          detail.TenderDocId.ShouldBe(doc.Id);
      }

      [Fact]
      public async Task Delete_Should_Remove_Task_And_Storage_Objects()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
          var doc = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf",
              new MemoryStream(new byte[] { 1 }));

          await _appService.DeleteAsync(task.Id);

          var repo = GetRequiredService<IRepository<CompareTask, Guid>>();
          (await repo.FindAsync(task.Id)).ShouldBeNull();
          _fileStorage.Objects.Keys.Any(k => k.Contains(doc.Id.ToString())).ShouldBeFalse();
      }
  }
  ```

  修改 `test/DredgeAI.BidCompare.Application.Tests/BidCompareApplicationTestModule.cs` 为如下完整内容（后续 Task 会在标注处继续追加 Replace 行）：

  ```csharp
  using DredgeAI.BidCompare.EntityFrameworkCore;
  using DredgeAI.BidCompare.Storage;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.DependencyInjection.Extensions;
  using Volo.Abp.BackgroundJobs;
  using Volo.Abp.Modularity;

  namespace DredgeAI.BidCompare;

  [DependsOn(
      typeof(BidCompareApplicationModule),
      typeof(BidCompareEntityFrameworkCoreTestModule)
  )]
  public class BidCompareApplicationTestModule : AbpModule
  {
      public override void ConfigureServices(ServiceConfigurationContext context)
      {
          context.Services.Replace(ServiceDescriptor.Singleton<IBackgroundJobManager, RecordingBackgroundJobManager>());
          context.Services.Replace(ServiceDescriptor.Singleton<IFileStorage, InMemoryFileStorage>());
          // [Task8] IAnGineerClient / [Task9] ICompareAlgoClient / [Task11] ILlmGateway / [Task14] IPdfConverter 的 Fake 在此追加
      }
  }
  ```

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter CompareTaskAppServiceTests
  ```

  预期：**编译失败**（DTO/AppService 不存在）。

- [ ] **Step 2: 创建 Contracts DTO 与 AppService 接口**

  `CompareTasks/CompareTaskDto.cs`（字段名逐字遵守 spec §6.1：`id, name, status, docIds, tenderDocId, clauseSnapshot, progress, createdAt`）：

  ```csharp
  using System;
  using System.Collections.Generic;
  using DredgeAI.BidCompare.Clauses;
  using Volo.Abp.Application.Dtos;

  namespace DredgeAI.BidCompare.CompareTasks;

  public class CompareTaskDto : EntityDto<Guid>
  {
      public string Name { get; set; } = default!;

      public CompareTaskStatus Status { get; set; }

      public List<Guid> DocIds { get; set; } = new();

      public Guid? TenderDocId { get; set; }

      public List<ClauseDto>? ClauseSnapshot { get; set; }

      public CompareProgressDto Progress { get; set; } = new();

      public DateTime CreatedAt { get; set; }
  }
  ```

  `CompareTasks/CompareProgressDto.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.CompareTasks;

  public class CompareProgressDto
  {
      public string Stage { get; set; } = "parsing";

      public int Percent { get; set; }

      public string? Message { get; set; }
  }
  ```

  `CompareTasks/CreateCompareTaskDto.cs`：

  ```csharp
  using System.Collections.Generic;
  using System.ComponentModel.DataAnnotations;
  using DredgeAI.BidCompare.Clauses;

  namespace DredgeAI.BidCompare.CompareTasks;

  public class CreateCompareTaskDto
  {
      [Required]
      [StringLength(128)]
      public string Name { get; set; } = default!;

      /// <summary>spec §6「创建任务（含条款清单快照）」：可选，提供即锁定快照。</summary>
      public List<ClauseInputDto>? Clauses { get; set; }
  }
  ```

  `CompareTasks/GetCompareTasksInput.cs`：

  ```csharp
  using Volo.Abp.Application.Dtos;

  namespace DredgeAI.BidCompare.CompareTasks;

  public class GetCompareTasksInput : PagedAndSortedResultRequestDto
  {
      public string? Name { get; set; }

      public CompareTaskStatus? Status { get; set; }
  }
  ```

  `Documents/CompareDocumentDto.cs`：

  ```csharp
  using System;
  using Volo.Abp.Application.Dtos;

  namespace DredgeAI.BidCompare.Documents;

  public class CompareDocumentDto : EntityDto<Guid>
  {
      public Guid TaskId { get; set; }

      public DocumentRole Role { get; set; }

      public string FileName { get; set; } = default!;

      public long FileSize { get; set; }

      public DocumentParseStatus ParseStatus { get; set; }

      public string? ParseError { get; set; }

      public int? PageCount { get; set; }

      public double? OcrLowConfidenceRatio { get; set; }

      public DateTime CreatedAt { get; set; }
  }
  ```

  `Clauses/ClauseDto.cs`（spec §6.1 Clause 逐字段）：

  ```csharp
  namespace DredgeAI.BidCompare.Clauses;

  public class ClauseDto
  {
      public string ClauseId { get; set; } = default!;

      public ClauseSource Source { get; set; }

      public string Text { get; set; } = default!;

      public bool Mandatory { get; set; }

      public string? Category { get; set; }
  }
  ```

  `Clauses/ClauseInputDto.cs`（创建任务 / PUT clauses 共用入参）：

  ```csharp
  using System.ComponentModel.DataAnnotations;

  namespace DredgeAI.BidCompare.Clauses;

  public class ClauseInputDto
  {
      /// <summary>可空：新增条款由服务端生成；从草案/模板带过来的条款保留原 id。</summary>
      public string? ClauseId { get; set; }

      /// <summary>可空：默认 Manual（extracted/template 由前端透传）。</summary>
      public ClauseSource? Source { get; set; }

      [Required]
      [StringLength(2000)]
      public string Text { get; set; } = default!;

      public bool Mandatory { get; set; } = true;

      [StringLength(64)]
      public string? Category { get; set; }
  }
  ```

  `CompareTasks/ICompareTaskAppService.cs`（本 Task 先含任务/文档部分；Task 8/9/11/13/14 会在此接口追加方法，届时给出追加后的完整签名清单）：

  ```csharp
  using System;
  using System.IO;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.Documents;
  using Volo.Abp.Application.Dtos;
  using Volo.Abp.Application.Services;

  namespace DredgeAI.BidCompare.CompareTasks;

  public interface ICompareTaskAppService : IApplicationService
  {
      Task<CompareTaskDto> CreateAsync(CreateCompareTaskDto input);

      Task<CompareTaskDto> GetAsync(Guid id);

      Task<PagedResultDto<CompareTaskDto>> GetListAsync(GetCompareTasksInput input);

      Task DeleteAsync(Guid id);

      Task<CompareDocumentDto> UploadDocumentAsync(Guid id, DocumentRole role, string fileName, Stream content);
  }
  ```

  `Application/BackgroundJobs/ParseDocumentArgs.cs`（Job 类在 Task 8 实现，本 Task 仅定义参数类型供上传时入队）：

  ```csharp
  using System;

  namespace DredgeAI.BidCompare.BackgroundJobs;

  public class ParseDocumentArgs
  {
      public Guid TaskId { get; set; }

      public Guid DocumentId { get; set; }
  }
  ```

- [ ] **Step 3: 实现 CompareTaskAppService**

  创建 `src/DredgeAI.BidCompare.Application/CompareTasks/CompareTaskAppService.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text.Json;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.BackgroundJobs;
  using DredgeAI.BidCompare.Clauses;
  using DredgeAI.BidCompare.Documents;
  using DredgeAI.BidCompare.Evidences;
  using DredgeAI.BidCompare.Storage;
  using Volo.Abp;
  using Volo.Abp.Application.Dtos;
  using Volo.Abp.Application.Services;
  using Volo.Abp.BackgroundJobs;
  using Volo.Abp.Domain.Repositories;

  namespace DredgeAI.BidCompare.CompareTasks;

  [RemoteService(false)] // 精确路由由 HttpApi 显式 Controller 暴露
  public class CompareTaskAppService : ApplicationService, ICompareTaskAppService
  {
      private static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx" };
      private const int MaxBidDocuments = 5;

      internal static readonly JsonSerializerOptions SnapshotJsonOptions = new()
      {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          PropertyNameCaseInsensitive = true
      };

      private readonly IRepository<CompareTask, Guid> _taskRepository;
      private readonly IRepository<CompareDocument, Guid> _documentRepository;
      private readonly IRepository<EvidenceItem, Guid> _evidenceRepository;
      private readonly IFileStorage _fileStorage;
      private readonly IBackgroundJobManager _backgroundJobManager;

      public CompareTaskAppService(
          IRepository<CompareTask, Guid> taskRepository,
          IRepository<CompareDocument, Guid> documentRepository,
          IRepository<EvidenceItem, Guid> evidenceRepository,
          IFileStorage fileStorage,
          IBackgroundJobManager backgroundJobManager)
      {
          _taskRepository = taskRepository;
          _documentRepository = documentRepository;
          _evidenceRepository = evidenceRepository;
          _fileStorage = fileStorage;
          _backgroundJobManager = backgroundJobManager;
      }

      public async Task<CompareTaskDto> CreateAsync(CreateCompareTaskDto input)
      {
          var task = new CompareTask(GuidGenerator.Create(), input.Name.Trim());
          if (input.Clauses is { Count: > 0 })
          {
              var snapshot = BuildSnapshot(input.Clauses);
              task.LockClauseSnapshot(JsonSerializer.Serialize(snapshot, SnapshotJsonOptions));
          }

          await _taskRepository.InsertAsync(task, autoSave: true);
          return MapToDto(task, new List<CompareDocument>());
      }

      public async Task<CompareTaskDto> GetAsync(Guid id)
      {
          var task = await _taskRepository.GetAsync(id);
          var documents = await GetTaskDocumentsAsync(id);
          return MapToDto(task, documents);
      }

      public async Task<PagedResultDto<CompareTaskDto>> GetListAsync(GetCompareTasksInput input)
      {
          var queryable = await _taskRepository.GetQueryableAsync();
          queryable = queryable
              .WhereIf(!input.Name.IsNullOrWhiteSpace(), x => x.Name.Contains(input.Name!))
              .WhereIf(input.Status.HasValue, x => x.Status == input.Status!.Value);

          var totalCount = await AsyncExecuter.CountAsync(queryable);
          var tasks = await AsyncExecuter.ToListAsync(queryable
              .OrderByDescending(x => x.CreationTime)
              .PageBy(input.SkipCount, input.MaxResultCount));

          var taskIds = tasks.Select(x => x.Id).ToList();
          var docQueryable = await _documentRepository.GetQueryableAsync();
          var documents = await AsyncExecuter.ToListAsync(docQueryable.Where(d => taskIds.Contains(d.TaskId)));

          var items = tasks
              .Select(t => MapToDto(t, documents.Where(d => d.TaskId == t.Id).ToList()))
              .ToList();

          return new PagedResultDto<CompareTaskDto>(totalCount, items);
      }

      public async Task DeleteAsync(Guid id)
      {
          var task = await _taskRepository.GetAsync(id);
          var documents = await GetTaskDocumentsAsync(id);

          foreach (var document in documents)
          {
              await DeleteStorageQuietlyAsync(document.OriginStorageKey);
              if (document.IrStorageKey != null) await DeleteStorageQuietlyAsync(document.IrStorageKey);
              if (document.DocMdStorageKey != null) await DeleteStorageQuietlyAsync(document.DocMdStorageKey);
          }

          var evidenceQueryable = await _evidenceRepository.GetQueryableAsync();
          var evidences = await AsyncExecuter.ToListAsync(evidenceQueryable.Where(e => e.TaskId == id));
          await _evidenceRepository.DeleteManyAsync(evidences, autoSave: true);
          await _documentRepository.DeleteManyAsync(documents, autoSave: true);
          await _taskRepository.DeleteAsync(task, autoSave: true);
      }

      public async Task<CompareDocumentDto> UploadDocumentAsync(Guid id, DocumentRole role, string fileName, Stream content)
      {
          var task = await _taskRepository.GetAsync(id);

          var extension = Path.GetExtension(fileName).ToLowerInvariant();
          if (!AllowedExtensions.Contains(extension))
          {
              throw new BusinessException(BidCompareErrorCodes.UnsupportedFileType)
                  .WithData("extension", extension);
          }

          var queryable = await _documentRepository.GetQueryableAsync();
          var bidCount = await AsyncExecuter.CountAsync(
              queryable.Where(d => d.TaskId == id && d.Role == DocumentRole.Bid));
          if (role == DocumentRole.Bid && bidCount >= MaxBidDocuments)
          {
              throw new BusinessException(BidCompareErrorCodes.DocumentCountOutOfRange)
                  .WithData("min", 2)
                  .WithData("max", MaxBidDocuments);
          }

          using var buffer = new MemoryStream();
          await content.CopyToAsync(buffer);
          var bytes = buffer.ToArray();

          var documentId = GuidGenerator.Create();
          var storageKey = $"compare/{id}/{documentId}/origin{extension}";
          await _fileStorage.UploadAsync(storageKey, new MemoryStream(bytes), ContentTypeOf(extension));

          var document = new CompareDocument(documentId, id, role, Path.GetFileName(fileName), bytes.Length, storageKey);
          await _documentRepository.InsertAsync(document, autoSave: true);

          if (role == DocumentRole.Tender)
          {
              task.SetTenderDocument(documentId);
              await _taskRepository.UpdateAsync(task, autoSave: true);
          }

          await _backgroundJobManager.EnqueueAsync(new ParseDocumentArgs { TaskId = id, DocumentId = documentId });

          return MapToDto(document);
      }

      internal static List<ClauseSnapshotItem> BuildSnapshot(IEnumerable<ClauseInputDto> clauses)
      {
          return clauses.Select(c => new ClauseSnapshotItem
          {
              ClauseId = c.ClauseId.IsNullOrWhiteSpace() ? Guid.NewGuid().ToString("N") : c.ClauseId!,
              Source = c.Source ?? ClauseSource.Manual,
              Text = c.Text.Trim(),
              Mandatory = c.Mandatory,
              Category = c.Category
          }).ToList();
      }

      internal static CompareTaskDto MapToDto(CompareTask task, List<CompareDocument> documents)
      {
          return new CompareTaskDto
          {
              Id = task.Id,
              Name = task.Name,
              Status = task.Status,
              DocIds = documents.OrderBy(d => d.CreationTime).Select(d => d.Id).ToList(),
              TenderDocId = task.TenderDocumentId,
              ClauseSnapshot = task.ClauseSnapshotJson == null
                  ? null
                  : JsonSerializer.Deserialize<List<ClauseDto>>(task.ClauseSnapshotJson, SnapshotJsonOptions),
              Progress = new CompareProgressDto
              {
                  Stage = task.ProgressStage,
                  Percent = task.ProgressPercent,
                  Message = task.ProgressMessage
              },
              CreatedAt = task.CreationTime
          };
      }

      internal static CompareDocumentDto MapToDto(CompareDocument document)
      {
          return new CompareDocumentDto
          {
              Id = document.Id,
              TaskId = document.TaskId,
              Role = document.Role,
              FileName = document.FileName,
              FileSize = document.FileSize,
              ParseStatus = document.ParseStatus,
              ParseError = document.ParseError,
              PageCount = document.PageCount,
              OcrLowConfidenceRatio = document.OcrLowConfidenceRatio,
              CreatedAt = document.CreationTime
          };
      }

      private async Task<List<CompareDocument>> GetTaskDocumentsAsync(Guid taskId)
      {
          var queryable = await _documentRepository.GetQueryableAsync();
          return await AsyncExecuter.ToListAsync(queryable.Where(d => d.TaskId == taskId));
      }

      private async Task DeleteStorageQuietlyAsync(string key)
      {
          try
          {
              await _fileStorage.DeleteAsync(key);
          }
          catch
          {
              // 对象存储删除失败不阻塞任务删除（孤儿对象由运维清理）
          }
      }

      private static string ContentTypeOf(string extension) => extension switch
      {
          ".pdf" => "application/pdf",
          ".doc" => "application/msword",
          ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
          _ => "application/octet-stream"
      };
  }
  ```

- [ ] **Step 4: 实现显式 Controller（精确路由）**

  创建 `src/DredgeAI.BidCompare.HttpApi/Controllers/CompareTaskController.cs`（本 Task 版本；Task 8/9/11/13/14 会追加 action，到时给出追加代码块）：

  ```csharp
  using System;
  using System.ComponentModel.DataAnnotations;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.CompareTasks;
  using DredgeAI.BidCompare.Documents;
  using Microsoft.AspNetCore.Http;
  using Microsoft.AspNetCore.Mvc;
  using Volo.Abp.Application.Dtos;
  using Volo.Abp.AspNetCore.Mvc;

  namespace DredgeAI.BidCompare.Controllers;

  [Area("compare")]
  [Route("api/compare/tasks")]
  public class CompareTaskController : AbpControllerBase
  {
      private readonly ICompareTaskAppService _appService;

      public CompareTaskController(ICompareTaskAppService appService)
      {
          _appService = appService;
      }

      /// <summary>POST /api/compare/tasks 创建任务（含条款清单快照）</summary>
      [HttpPost]
      public Task<CompareTaskDto> CreateAsync([FromBody] CreateCompareTaskDto input)
          => _appService.CreateAsync(input);

      /// <summary>GET /api/compare/tasks/{id} 任务详情 + 状态机状态 + 各阶段进度</summary>
      [HttpGet("{id}")]
      public Task<CompareTaskDto> GetAsync(Guid id)
          => _appService.GetAsync(id);

      /// <summary>GET /api/compare/tasks 任务列表（分页，PagedResultDto）</summary>
      [HttpGet]
      public Task<PagedResultDto<CompareTaskDto>> GetListAsync([FromQuery] GetCompareTasksInput input)
          => _appService.GetListAsync(input);

      /// <summary>DELETE /api/compare/tasks/{id}（补充路由，spec §7.1 操作列删除）</summary>
      [HttpDelete("{id}")]
      public async Task<IActionResult> DeleteAsync(Guid id)
      {
          await _appService.DeleteAsync(id);
          return NoContent();
      }

      /// <summary>POST /api/compare/tasks/{id}/documents 上传文档（标书/招标文件，区分 role）</summary>
      [HttpPost("{id}/documents")]
      [RequestSizeLimit(200 * 1024 * 1024)] // 单份标书 100~500 页 PDF，放宽到 200MB
      public async Task<CompareDocumentDto> UploadDocumentAsync(Guid id, [FromForm] UploadDocumentForm form)
      {
          await using var stream = form.File.OpenReadStream();
          return await _appService.UploadDocumentAsync(id, form.Role, form.File.FileName, stream);
      }
  }

  public class UploadDocumentForm
  {
      [Required]
      public IFormFile File { get; set; } = default!;

      /// <summary>0=Bid 标书（默认），1=Tender 招标文件。</summary>
      [FromForm]
      public DocumentRole Role { get; set; } = DocumentRole.Bid;
  }
  ```

- [ ] **Step 5: 跑测试确认通过**

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter CompareTaskAppServiceTests
  ```

  预期：8 passed。

- [ ] **Step 6: 启动宿主用 swagger 冒烟验证路由形状**

  ```bash
  dotnet run --project src/DredgeAI.BidCompare.HttpApi.Host &
  curl -s http://localhost:44342/swagger/v1/swagger.json | grep -o '"/api/compare/tasks[^"]*"'
  kill %1
  ```

  预期输出含：`/api/compare/tasks`、`/api/compare/tasks/{id}`、`/api/compare/tasks/{id}/documents`。

- [ ] **Step 7: 提交**

  ```bash
  git add backend/DredgeAI.BidCompare
  git commit -m "feat(backend): add compare task CRUD and document upload API (/api/compare/tasks)"
  ```

---

## Task 8 【P1】AnGIneer 对接：产物映射与内部适配 IR 校验、解析后台任务、IR 查询 API

覆盖 spec §6 路由：`GET /api/compare/tasks/{id}/ir/{docId}`；实现 v2 消费契约（`doc_blocks_graph.jsonl` + `doc_blocks_graph_meta.json` + `content.md` + `images/` → 内部适配 IR）与 v2 §2/§4/§5 硬性要求校验、spec §4.5 OCR 置信度统计（source/confidence 缺失时跳过）。

**Files:**
- Create: `src/DredgeAI.BidCompare.Domain/AnGineer/IAnGineerClient.cs`
- Create: `src/DredgeAI.BidCompare.Domain/Documents/AnGineerIrMapper.cs`
- Create: `src/DredgeAI.BidCompare.Domain/Documents/IIrValidator.cs`
- Create: `src/DredgeAI.BidCompare.Domain/Documents/IrValidationResult.cs`
- Create: `src/DredgeAI.BidCompare.Domain/Documents/IrValidator.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/Ir/DocumentIrDtos.cs`
- Create: `src/DredgeAI.BidCompare.Application/BackgroundJobs/ParseDocumentJob.cs`
- Modify: `src/DredgeAI.BidCompare.Application.Contracts/CompareTasks/ICompareTaskAppService.cs`（追加 `GetDocumentIrAsync`）
- Modify: `src/DredgeAI.BidCompare.Application/CompareTasks/CompareTaskAppService.cs`（追加 `GetDocumentIrAsync`）
- Modify: `src/DredgeAI.BidCompare.HttpApi/Controllers/CompareTaskController.cs`（追加 ir action）
- Create: `src/DredgeAI.BidCompare.HttpApi.Host/AnGineer/AnGineerOptions.cs`
- Create: `src/DredgeAI.BidCompare.HttpApi.Host/AnGineer/HttpAnGineerClient.cs`
- Modify: `src/DredgeAI.BidCompare.HttpApi.Host/BidCompareHttpApiHostModule.cs`
- Modify: `src/DredgeAI.BidCompare.HttpApi.Host/appsettings.json`
- Test: `test/DredgeAI.BidCompare.TestBase/Fakes/FakeAnGineerClient.cs`
- Test: `test/DredgeAI.BidCompare.TestBase/Fakes/SampleIr.cs`
- Modify: `test/DredgeAI.BidCompare.Application.Tests/BidCompareApplicationTestModule.cs`
- Test: `test/DredgeAI.BidCompare.Domain.Tests/Documents/IrValidatorTests.cs`
- Test: `test/DredgeAI.BidCompare.Domain.Tests/Documents/AnGineerIrMapperTests.cs`
- Test: `test/DredgeAI.BidCompare.Application.Tests/BackgroundJobs/ParseDocumentJobTests.cs`

**Steps:**

- [ ] **Step 1: 写失败测试（AnGineerIrMapper 字段/类型映射 + IrValidator 覆盖 v2 硬性要求）**

  创建 `test/DredgeAI.BidCompare.TestBase/Fakes/SampleIr.cs`（AnGIneer 原始产物样例 + 映射后的内部适配 IR 样例，测试工程共用）：

  ```csharp
  namespace DredgeAI.BidCompare;

  /// <summary>
  /// v2 消费要求的样例数据：ValidGraphJsonl / ValidMetaJson 为 AnGIneer 原始产物
  /// （doc_blocks_graph.jsonl / doc_blocks_graph_meta.json），Valid 为 AnGineerIrMapper
  /// 映射后的内部适配 IR（bbox 0~1 归一化、blockId=block_uid）。
  /// </summary>
  public static class SampleIr
  {
      /// <summary>AnGIneer doc_blocks_graph.jsonl 样例（3 行，块级字段见 v2 §1）。</summary>
      public const string ValidGraphJsonl = """
      {"block_uid":"b0001","block_type":"title","page_idx":0,"plain_text":"第三章 技术方案","derived_level":1,"bbox":[0.0672,0.0594,0.9244,0.095],"source":"native","confidence":1.0}
      {"block_uid":"b0002","block_type":"table","page_idx":1,"plain_text":"报价表","derived_level":0,"bbox":[0.0672,0.1188,0.9244,0.2969],"table_html":"<table><tr><td>总价</td></tr></table>","image_path":"images/t1.jpg","source":"native","confidence":1.0}
      {"block_uid":"b0003","block_type":"paragraph","page_idx":1,"plain_text":"盖章扫描文字","derived_level":0,"bbox":[0.0672,0.3563,0.9244,0.4157],"source":"ocr","confidence":0.3}
      """;

      /// <summary>AnGIneer doc_blocks_graph_meta.json 样例（outlines / docMeta / pages，v2 §1）。</summary>
      public const string ValidMetaJson = """
      {
        "build_id": "demo-build",
        "outlines": [
          { "title": "第三章 技术方案", "level": 1, "block_uid": "b0001", "children": [] }
        ],
        "docMeta": {
          "fileName": "标书A.pdf", "pageCount": 2,
          "author": null, "creatorTool": "Microsoft Word",
          "createdAt": null, "modifiedAt": null
        },
        "pages": [
          { "page_idx": 0, "width": 1190, "height": 1684 },
          { "page_idx": 1, "width": 1190, "height": 1684 }
        ]
      }
      """;

      /// <summary>映射后的内部适配 IR（docId 由调用方传入；页面 1190×1684 为真实尺寸，bbox 为 0~1 归一化值）。</summary>
      public const string Valid = """
      {
        "schemaVersion": "2.0",
        "docId": "doc-a",
        "meta": {
          "fileName": "标书A.pdf",
          "pageCount": 2,
          "author": null,
          "creatorTool": "Microsoft Word",
          "createdAt": null,
          "modifiedAt": null
        },
        "pages": [
          { "pageIdx": 0, "width": 1190, "height": 1684 },
          { "pageIdx": 1, "width": 1190, "height": 1684 }
        ],
        "outline": [
          { "title": "第三章 技术方案", "level": 1, "blockId": "b0001", "children": [] }
        ],
        "blocks": [
          {
            "blockId": "b0001", "pageIdx": 0, "bbox": [0.0672, 0.0594, 0.9244, 0.095],
            "type": "title", "text": "第三章 技术方案", "textLevel": 1,
            "source": "native", "confidence": 1.0
          },
          {
            "blockId": "b0002", "pageIdx": 1, "bbox": [0.0672, 0.1188, 0.9244, 0.2969],
            "type": "table", "text": "报价表", "textLevel": 0,
            "source": "native", "confidence": 1.0,
            "table": { "html": "<table><tr><td>总价</td></tr></table>", "imgPath": "images/t1.jpg" }
          },
          {
            "blockId": "b0003", "pageIdx": 1, "bbox": [0.0672, 0.3563, 0.9244, 0.4157],
            "type": "para", "text": "盖章扫描文字", "textLevel": 0,
            "source": "ocr", "confidence": 0.3
          }
        ]
      }
      """;

      public const string ValidContentMd = "# 第三章 技术方案\n\n本方案……\n";
  }
  ```

  创建 `test/DredgeAI.BidCompare.Domain.Tests/Documents/AnGineerIrMapperTests.cs`：

  ```csharp
  using System.Text.Json.Nodes;
  using DredgeAI.BidCompare.Documents;
  using Shouldly;
  using Xunit;

  namespace DredgeAI.BidCompare.Documents;

  public class AnGineerIrMapperTests
  {
      [Fact]
      public void Map_Should_Produce_Internal_Ir_Per_V2_Field_Mapping()
      {
          var irJson = AnGineerIrMapper.MapToIrJson(SampleIr.ValidGraphJsonl, SampleIr.ValidMetaJson, "doc-a");

          // 与期望的内部适配 IR 深度一致（v2 §2 字段映射 + §3 类型映射）
          JsonNode.DeepEquals(JsonNode.Parse(irJson), JsonNode.Parse(SampleIr.Valid)).ShouldBeTrue();
      }

      [Fact]
      public void Map_Should_Apply_Type_Mapping_And_Block_Uid()
      {
          var irJson = AnGineerIrMapper.MapToIrJson(SampleIr.ValidGraphJsonl, SampleIr.ValidMetaJson, "doc-a");
          var node = JsonNode.Parse(irJson)!;

          // paragraph → para（v2 §3）；blockId 直接采用 block_uid（v2 §2）
          node["blocks"]![2]!["type"]!.GetValue<string>().ShouldBe("para");
          node["blocks"]![0]!["blockId"]!.GetValue<string>().ShouldBe("b0001");
          // 表格：table_html/image_path → table.html/table.imgPath
          node["blocks"]![1]!["table"]!["html"]!.GetValue<string>().ShouldContain("<table>");
          node["blocks"]![1]!["table"]!["imgPath"]!.GetValue<string>().ShouldBe("images/t1.jpg");
      }

      [Fact]
      public void Map_Should_Tolerate_Missing_Source_And_Confidence()
      {
          // v2 §4：AnGIneer 补齐字段之前 source/confidence 缺省 → 映射为 null
          var jsonl = "{\"block_uid\":\"b1\",\"block_type\":\"paragraph\",\"page_idx\":0,\"plain_text\":\"正文\",\"bbox\":[0.1,0.1,0.9,0.2]}";
          var irJson = AnGineerIrMapper.MapToIrJson(jsonl, SampleIr.ValidMetaJson, "doc-a");
          var node = JsonNode.Parse(irJson)!;

          node["blocks"]![0]!["source"]!.GetValue<string?>().ShouldBeNull();
          node["blocks"]![0]!["confidence"]!.GetValue<double?>().ShouldBeNull();
      }
  }
  ```

  创建 `test/DredgeAI.BidCompare.Domain.Tests/Documents/IrValidatorTests.cs`：

  ```csharp
  using System.Text.Json;
  using DredgeAI.BidCompare.Documents;
  using Shouldly;
  using Xunit;

  namespace DredgeAI.BidCompare.Documents;

  public class IrValidatorTests
  {
      private readonly IrValidator _validator = new();

      [Fact]
      public void Valid_Sample_Should_Pass()
      {
          var result = _validator.Validate(SampleIr.Valid);
          result.IsValid.ShouldBeTrue(string.Join("; ", result.Errors));
      }

      [Fact]
      public void Invalid_Json_Should_Fail()
      {
          _validator.Validate("{oops").IsValid.ShouldBeFalse();
      }

      [Fact]
      public void Missing_Required_Fields_Should_Fail()
      {
          var result = _validator.Validate("""{"schemaVersion":"1.0"}""");
          result.IsValid.ShouldBeFalse();
          result.Errors.ShouldContain(e => e.Contains("docId"));
          result.Errors.ShouldContain(e => e.Contains("meta"));
          result.Errors.ShouldContain(e => e.Contains("blocks"));
      }

      [Fact]
      public void Pixel_Bbox_Should_Be_Rejected() // v2 §2：bbox 为 0~1 归一化坐标，像素坐标拒收
      {
          var ir = SampleIr.Valid.Replace("[0.0672, 0.0594, 0.9244, 0.095]", "[80, 100, 1100, 160]");
          var result = _validator.Validate(ir);
          result.IsValid.ShouldBeFalse();
          result.Errors.ShouldContain(e => e.Contains("归一化"));
      }

      [Fact]
      public void Bbox_Above_One_Should_Fail()
      {
          var ir = SampleIr.Valid.Replace("[0.0672, 0.0594, 0.9244, 0.095]", "[0, 0, 1.5, 0.095]");
          var result = _validator.Validate(ir);
          result.IsValid.ShouldBeFalse();
          result.Errors.ShouldContain(e => e.Contains("bbox"));
      }

      [Fact]
      public void Null_Source_And_Confidence_Should_Pass() // v2 §4：AnGIneer 补齐前允许缺省/null
      {
          var ir = SampleIr.Valid
              .Replace(", \"source\": \"native\", \"confidence\": 1.0", "")
              .Replace(", \"source\": \"ocr\", \"confidence\": 0.3", "");
          var result = _validator.Validate(ir);
          result.IsValid.ShouldBeTrue(string.Join("; ", result.Errors));
      }

      [Fact]
      public void Duplicate_BlockId_Should_Fail() // v2 §2：文档内唯一（= block_uid）
      {
          var ir = SampleIr.Valid.Replace("\"blockId\": \"b0003\"", "\"blockId\": \"b0002\"");
          _validator.Validate(ir).IsValid.ShouldBeFalse();
      }

      [Fact]
      public void Table_Without_Html_Or_Screenshot_Should_Fail() // spec §4.3-4
      {
          var ir = SampleIr.Valid.Replace(
              "\"table\": { \"html\": \"<table><tr><td>总价</td></tr></table>\", \"imgPath\": \"images/t1.jpg\" }",
              "\"table\": { \"html\": \"\", \"imgPath\": \"\" }");
          var result = _validator.Validate(ir);
          result.IsValid.ShouldBeFalse();
          result.Errors.ShouldContain(e => e.Contains("table.html"));
      }

      [Fact]
      public void Confidence_Out_Of_Range_Should_Fail() // v2 §4：存在时须在 0~1
      {
          var ir = SampleIr.Valid.Replace("\"confidence\": 1.0", "\"confidence\": 1.7");
          _validator.Validate(ir).IsValid.ShouldBeFalse();
      }

      [Fact]
      public void Ocr_LowConfidence_Ratio_Should_Be_Calculated() // spec §4.5
      {
          using var doc = JsonDocument.Parse(SampleIr.Valid);
          var ratio = IrValidator.CalculateOcrLowConfidenceRatio(doc.RootElement);
          ratio.ShouldBe(1.0 / 3.0, 0.001);
      }
  }
  ```

  ```bash
  dotnet test test/DredgeAI.BidCompare.Domain.Tests --filter IrValidatorTests
  ```

  预期：**编译失败**。

- [ ] **Step 2: 实现 AnGineerIrMapper + IIrValidator / IrValidationResult / IrValidator**

  `src/DredgeAI.BidCompare.Domain/Documents/AnGineerIrMapper.cs`：

  ```csharp
  using System.Collections.Generic;
  using System.Linq;
  using System.Text.Json;
  using System.Text.Json.Serialization;

  namespace DredgeAI.BidCompare.Documents;

  /// <summary>
  /// AnGIneer 产物 → 内部适配 IR 映射（v2 文档 §2 字段映射 + §3 类型映射）。
  /// 输入 doc_blocks_graph.jsonl 与 doc_blocks_graph_meta.json 的文本内容；
  /// 输出内部适配 IR JSON（blockId=block_uid、bbox 0~1 归一化直收、source/confidence 可空透传）。
  /// 纯静态无依赖，Domain 层单测覆盖。
  /// </summary>
  public static class AnGineerIrMapper
  {
      // v2 §3 类型映射表；page_number 忽略（或归入 header/footer，此处按「忽略」处理）
      private static readonly Dictionary<string, string> TypeMap = new()
      {
          ["title"] = "title",
          ["paragraph"] = "para",
          ["list"] = "list",
          ["table"] = "table",
          ["equation_interline"] = "equation",
          ["image"] = "image",
          ["figure"] = "image",
          ["page_header"] = "header",
          ["page_footer"] = "footer"
      };

      public static string MapToIrJson(string graphJsonl, string metaJson, string docId)
      {
          var meta = JsonSerializer.Deserialize<MetaDoc>(metaJson) ?? new MetaDoc();

          var blocks = new List<Dictionary<string, object?>>();
          foreach (var line in graphJsonl.Split('\n', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries))
          {
              var node = JsonSerializer.Deserialize<GraphNode>(line)
                  ?? throw new JsonException("doc_blocks_graph.jsonl 存在空行");
              var rawType = node.BlockType ?? "";
              if (rawType == "page_number")
              {
                  continue; // v2 §3：忽略
              }
              var type = TypeMap.TryGetValue(rawType, out var mapped) ? mapped : "para";

              var block = new Dictionary<string, object?>
              {
                  ["blockId"] = node.BlockUid,
                  ["pageIdx"] = node.PageIdx,
                  ["bbox"] = node.Bbox,
                  ["type"] = type,
                  ["text"] = ReadText(node, type),
                  // v2 §2：标题块 textLevel=derived_level，非标题固定 0
                  ["textLevel"] = type == "title" ? node.DerivedLevel ?? 1 : 0,
                  ["source"] = node.Source,       // v2 §4：补齐前为 null，透传
                  ["confidence"] = node.Confidence
              };
              if (type == "table")
              {
                  block["table"] = new Dictionary<string, object?>
                  {
                      ["html"] = node.TableHtml,
                      ["imgPath"] = node.ImagePath
                  };
              }
              if (type is "image" or "equation" && node.ImagePath != null)
              {
                  block["imgPath"] = node.ImagePath;
              }
              blocks.Add(block);
          }

          var ir = new Dictionary<string, object?>
          {
              ["schemaVersion"] = "2.0", // 内部适配 IR 版本（1.0 为已废止的 ir.json 交付契约）
              ["docId"] = docId,
              ["meta"] = meta.DocMeta ?? new Dictionary<string, object?>(),
              ["pages"] = (meta.Pages ?? new List<MetaPage>()).Select(p => new Dictionary<string, object?>
              {
                  ["pageIdx"] = p.PageIdx,
                  ["width"] = p.Width,
                  ["height"] = p.Height
              }).ToList(),
              ["outline"] = MapOutline(meta.Outlines),
              ["blocks"] = blocks
          };
          return JsonSerializer.Serialize(ir);
      }

      private static string? ReadText(GraphNode node, string type)
      {
          // v2 §2：公式块用 math_content / formula_body（LaTeX）
          if (type == "equation")
          {
              return node.MathContent ?? node.FormulaBody ?? node.PlainText;
          }
          return node.PlainText;
      }

      /// <summary>v2 §5-6：嵌套 outlines 直收；扁平结构（parent_outline_id）转嵌套 children。</summary>
      private static List<Dictionary<string, object?>> MapOutline(List<OutlineNode>? outlines)
      {
          if (outlines == null || outlines.Count == 0)
          {
              return new List<Dictionary<string, object?>>();
          }
          if (outlines.Any(o => o.Children is { Count: > 0 }))
          {
              return outlines.Select(ConvertOutlineNode).ToList();
          }
          if (outlines.All(o => o.ParentOutlineId == null))
          {
              return outlines.Select(ConvertOutlineNode).ToList();
          }
          var roots = outlines.Where(o => o.ParentOutlineId == null).ToList();
          return roots.Select(r => BuildOutlineNode(r, outlines)).ToList();
      }

      private static Dictionary<string, object?> ConvertOutlineNode(OutlineNode node) => new()
      {
          ["title"] = node.Title,
          ["level"] = node.Level,
          ["blockId"] = node.BlockUid ?? node.BlockId,
          ["children"] = (node.Children ?? new List<OutlineNode>()).Select(ConvertOutlineNode).ToList()
      };

      private static Dictionary<string, object?> BuildOutlineNode(OutlineNode node, List<OutlineNode> all) => new()
      {
          ["title"] = node.Title,
          ["level"] = node.Level,
          ["blockId"] = node.BlockUid ?? node.BlockId,
          ["children"] = all.Where(o => o.ParentOutlineId == node.OutlineId).Select(o => BuildOutlineNode(o, all)).ToList()
      };

      private class GraphNode
      {
          [JsonPropertyName("block_uid")] public string? BlockUid { get; set; }
          [JsonPropertyName("block_type")] public string? BlockType { get; set; }
          [JsonPropertyName("page_idx")] public int PageIdx { get; set; }
          [JsonPropertyName("plain_text")] public string? PlainText { get; set; }
          [JsonPropertyName("derived_level")] public int? DerivedLevel { get; set; }
          [JsonPropertyName("bbox")] public double[]? Bbox { get; set; }
          [JsonPropertyName("table_html")] public string? TableHtml { get; set; }
          [JsonPropertyName("math_content")] public string? MathContent { get; set; }
          [JsonPropertyName("formula_body")] public string? FormulaBody { get; set; }
          [JsonPropertyName("image_path")] public string? ImagePath { get; set; }
          [JsonPropertyName("source")] public string? Source { get; set; }
          [JsonPropertyName("confidence")] public double? Confidence { get; set; }
      }

      private class OutlineNode
      {
          [JsonPropertyName("title")] public string Title { get; set; } = "";
          [JsonPropertyName("level")] public int Level { get; set; }
          [JsonPropertyName("block_uid")] public string? BlockUid { get; set; }
          [JsonPropertyName("blockId")] public string? BlockId { get; set; }
          [JsonPropertyName("outline_id")] public string? OutlineId { get; set; }
          [JsonPropertyName("parent_outline_id")] public string? ParentOutlineId { get; set; }
          [JsonPropertyName("children")] public List<OutlineNode>? Children { get; set; }
      }

      private class MetaDoc
      {
          [JsonPropertyName("outlines")] public List<OutlineNode>? Outlines { get; set; }
          [JsonPropertyName("docMeta")] public Dictionary<string, object?>? DocMeta { get; set; }
          [JsonPropertyName("pages")] public List<MetaPage>? Pages { get; set; }
      }

      private class MetaPage
      {
          [JsonPropertyName("page_idx")] public int PageIdx { get; set; }
          [JsonPropertyName("width")] public double Width { get; set; }
          [JsonPropertyName("height")] public double Height { get; set; }
      }
  }
  ```

  `src/DredgeAI.BidCompare.Domain/Documents/IIrValidator.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.Documents;

  /// <summary>内部适配 IR 规范化校验（v2 文档 §2/§4/§5；spec §10 测试策略：不合格即拒收并报具体原因）。</summary>
  public interface IIrValidator
  {
      IrValidationResult Validate(string irJson);
  }
  ```

  `src/DredgeAI.BidCompare.Domain/Documents/IrValidationResult.cs`：

  ```csharp
  using System.Collections.Generic;
  using System.Linq;

  namespace DredgeAI.BidCompare.Documents;

  public class IrValidationResult
  {
      public IReadOnlyList<string> Errors { get; }

      public bool IsValid => Errors.Count == 0;

      public IrValidationResult(IEnumerable<string> errors)
      {
          Errors = errors.ToList();
      }
  }
  ```

  `src/DredgeAI.BidCompare.Domain/Documents/IrValidator.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text.Json;
  using Volo.Abp.DependencyInjection;

  namespace DredgeAI.BidCompare.Documents;

  /// <summary>
  /// 内部适配 IR 校验（v2 文档 §2/§4/§5 硬性要求，取代 spec §4.3）：
  /// 1) bbox 必须为 0~1 归一化坐标（拒绝像素坐标/负值/倒置）；
  /// 2) source/confidence 允许缺省（v2 §4 降级期）；存在时 source∈native|ocr、confidence∈[0,1]、native 恒 1.0；
  /// 3) blockId 文档内唯一（= AnGIneer block_uid）；
  /// 4) table 块必须同时给 html 与整表截图 imgPath；
  /// 另校验 schemaVersion/docId/meta/pages 必填与页面真实尺寸。
  /// </summary>
  public class IrValidator : IIrValidator, ITransientDependency
  {
      // seal 为保留类型（spec §4.3.5）：AnGIneer 当前不产出（v2 §3 映射表无此项）
      private static readonly HashSet<string> BlockTypes = new()
      {
          "title", "para", "table", "list", "image", "equation", "seal", "header", "footer"
      };

      public IrValidationResult Validate(string irJson)
      {
          var errors = new List<string>();
          JsonDocument document;
          try
          {
              document = JsonDocument.Parse(irJson);
          }
          catch (JsonException ex)
          {
              return new IrValidationResult(new[] { $"内部适配 IR 不是合法 JSON：{ex.Message}" });
          }

          using (document)
          {
              var root = document.RootElement;
              if (root.ValueKind != JsonValueKind.Object)
              {
                  return new IrValidationResult(new[] { "内部适配 IR 根节点必须是对象" });
              }

              if (!TryGetNonEmptyString(root, "schemaVersion", out _))
              {
                  errors.Add("缺少必填字段 schemaVersion");
              }
              if (!TryGetNonEmptyString(root, "docId", out _))
              {
                  errors.Add("缺少必填字段 docId");
              }

              if (!root.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
              {
                  errors.Add("缺少必填字段 pages（数组）");
              }
              else
              {
                  foreach (var page in pages.EnumerateArray())
                  {
                      if (!page.TryGetProperty("pageIdx", out var idx) || idx.ValueKind != JsonValueKind.Number)
                      {
                          errors.Add("pages[] 缺少 pageIdx");
                          continue;
                      }
                      var width = GetDouble(page, "width");
                      var height = GetDouble(page, "height");
                      if (width <= 0 || height <= 0)
                      {
                          errors.Add($"pages[{idx.GetInt32()}] width/height 必须为正数（页面真实尺寸，v2 §1 meta pages）");
                      }
                  }
              }

              if (!root.TryGetProperty("meta", out var meta) || meta.ValueKind != JsonValueKind.Object)
              {
                  errors.Add("缺少必填字段 meta");
              }
              else if (!TryGetNonEmptyString(meta, "fileName", out _))
              {
                  errors.Add("缺少必填字段 meta.fileName");
              }

              if (!root.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
              {
                  errors.Add("缺少必填字段 blocks（数组）");
              }
              else
              {
                  var seenBlockIds = new HashSet<string>();
                  foreach (var block in blocks.EnumerateArray())
                  {
                      ValidateBlock(block, seenBlockIds, errors);
                  }
              }
          }

          return new IrValidationResult(errors);
      }

      /// <summary>spec §4.5：source=ocr 且 confidence&lt;0.5 的块占比。v2 §4：source/confidence 缺失的块不参与统计；全部缺失时返回 0（提示降级关闭）。</summary>
      public static double CalculateOcrLowConfidenceRatio(JsonElement root)
      {
          if (!root.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
          {
              return 0;
          }

          var total = 0;
          var lowConfidence = 0;
          foreach (var block in blocks.EnumerateArray())
          {
              var source = block.TryGetProperty("source", out var s) && s.ValueKind == JsonValueKind.String
                  ? s.GetString() : null;
              var confidence = GetDouble(block, "confidence");
              if (source == null || confidence < 0)
              {
                  continue; // v2 §4：缺省块不参与（GetDouble 缺省约定返回 -1）
              }
              total++;
              if (source == "ocr" && confidence < 0.5)
              {
                  lowConfidence++;
              }
          }
          return total == 0 ? 0 : (double)lowConfidence / total;
      }

      private static void ValidateBlock(
          JsonElement block,
          HashSet<string> seenBlockIds,
          List<string> errors)
      {
          var label = "block";
          if (TryGetNonEmptyString(block, "blockId", out var blockId))
          {
              label = $"block[{blockId}]";
              if (!seenBlockIds.Add(blockId))
              {
                  errors.Add($"{label} blockId 重复（须文档内唯一）");
              }
          }
          else
          {
              errors.Add("block 缺少必填字段 blockId");
          }

          if (!block.TryGetProperty("pageIdx", out var pageIdxEl) || pageIdxEl.ValueKind != JsonValueKind.Number)
          {
              errors.Add($"{label} 缺少 pageIdx");
          }

          if (!block.TryGetProperty("bbox", out var bbox) || bbox.ValueKind != JsonValueKind.Array ||
              bbox.GetArrayLength() != 4)
          {
              errors.Add($"{label} bbox 必须为 [x0,y0,x1,y1] 四元数组");
          }
          else
          {
              var values = bbox.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : -1).ToArray();
              if (values.Any(v => v < 0) || values[2] <= values[0] || values[3] <= values[1])
              {
                  errors.Add($"{label} bbox 坐标非法（须 x1>x0 且 y1>y0，非负）");
              }
              else if (values.Any(v => v > 1.0))
              {
                  // v2 §2：bbox 为 0~1 归一化坐标，超出区间即疑似像素坐标，拒收
                  errors.Add($"{label} bbox 超出 0~1 归一化区间（疑似像素坐标，v2 不要求像素坐标）");
              }
          }

          if (!TryGetNonEmptyString(block, "type", out var type) || !BlockTypes.Contains(type))
          {
              errors.Add($"{label} type 非法（须为 {string.Join("|", BlockTypes)}）");
          }

          // v2 §4：source/confidence 允许缺省（AnGIneer 补齐前）；存在时才校验取值
          if (block.TryGetProperty("source", out var sourceEl) && sourceEl.ValueKind != JsonValueKind.Null)
          {
              if (!TryGetNonEmptyString(block, "source", out var source) || (source != "native" && source != "ocr"))
              {
                  errors.Add($"{label} source 必须为 native|ocr（v2 §4）");
              }
              else if (source == "native" &&
                       block.TryGetProperty("confidence", out var confEl) && confEl.ValueKind == JsonValueKind.Number &&
                       confEl.GetDouble() != 1.0)
              {
                  errors.Add($"{label} source=native 时 confidence 必须为 1.0（v2 §4）");
              }
          }

          var confidence = GetDouble(block, "confidence");
          if (confidence != -1 && (confidence < 0 || confidence > 1)) // GetDouble 缺省约定返回 -1
          {
              errors.Add($"{label} confidence 必须在 0~1（v2 §4）");
          }

          if (type == "table")
          {
              if (!block.TryGetProperty("table", out var table) || table.ValueKind != JsonValueKind.Object ||
                  !TryGetNonEmptyString(table, "html", out _))
              {
                  errors.Add($"{label} table 块缺少 table.html（spec §4.3-4）");
              }
              if (table.ValueKind != JsonValueKind.Object || !TryGetNonEmptyString(table, "imgPath", out _))
              {
                  errors.Add($"{label} table 块缺少 table.imgPath 整表截图（spec §4.3-4）");
              }
          }

          if (type is "image" or "seal" or "equation" && !TryGetNonEmptyString(block, "imgPath", out _))
          {
              errors.Add($"{label} {type} 块缺少 imgPath");
          }
      }

      private static bool TryGetNonEmptyString(JsonElement element, string property, out string value)
      {
          value = string.Empty;
          if (element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
          {
              value = prop.GetString() ?? string.Empty;
              return !string.IsNullOrWhiteSpace(value);
          }
          return false;
      }

      private static double GetDouble(JsonElement element, string property)
      {
          return element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.Number
              ? prop.GetDouble()
              : -1;
      }
  }
  ```

- [ ] **Step 3: 跑测试确认通过**

  ```bash
  dotnet test test/DredgeAI.BidCompare.Domain.Tests --filter IrValidatorTests
  ```

  预期：9 passed。

- [ ] **Step 4: 定义 IAnGineerClient 与 FakeAnGineerClient**

  创建 `src/DredgeAI.BidCompare.Domain/AnGineer/IAnGineerClient.cs`：

  ```csharp
  using System.Collections.Generic;
  using System.IO;
  using System.Threading;
  using System.Threading.Tasks;

  namespace DredgeAI.BidCompare.AnGineer;

  public enum AnGineerJobState
  {
      Processing = 0,
      Succeeded = 1,
      Failed = 2
  }

  /// <summary>AnGIneer 解析产物包（v2 §1 数据源：doc_blocks_graph.jsonl + doc_blocks_graph_meta.json + content.md + images/）。</summary>
  public record AnGineerPackage(
      byte[] GraphJsonl,
      byte[] MetaJson,
      byte[]? ContentMd,
      IReadOnlyDictionary<string, byte[]> Images);

  /// <summary>
  /// AnGIneer 解析流水线 adapter（提交文档 → 轮询 → 下载产物包）。
  /// 提供方部署形态变化只改实现，契约不变（spec §2 非目标：不约束提供方内部流水线）。
  /// </summary>
  public interface IAnGineerClient
  {
      /// <summary>提交解析任务，返回提供方任务 id。</summary>
      Task<string> SubmitAsync(string fileName, Stream content, CancellationToken cancellationToken = default);

      Task<AnGineerJobState> GetStateAsync(string jobId, CancellationToken cancellationToken = default);

      Task<AnGineerPackage> DownloadPackageAsync(string jobId, CancellationToken cancellationToken = default);
  }
  ```

  创建 `test/DredgeAI.BidCompare.TestBase/Fakes/FakeAnGineerClient.cs`：

  ```csharp
  using System.Collections.Generic;
  using System.IO;
  using System.Threading;
  using System.Threading.Tasks;

  namespace DredgeAI.BidCompare.AnGineer;

  /// <summary>
  /// 可编程 Fake：默认立即成功并返回 SampleIr 产物包；
  /// 设置 StateSequence 可模拟轮询过程，设置 FailWith 可模拟解析失败。
  /// </summary>
  public class FakeAnGineerClient : IAnGineerClient
  {
      public Queue<AnGineerJobState>? StateSequence { get; set; }

      public string? FailWith { get; set; }

      public AnGineerPackage Package { get; set; } = new(
          GraphJsonl: System.Text.Encoding.UTF8.GetBytes(SampleIr.ValidGraphJsonl),
          MetaJson: System.Text.Encoding.UTF8.GetBytes(SampleIr.ValidMetaJson),
          ContentMd: System.Text.Encoding.UTF8.GetBytes(SampleIr.ValidContentMd),
          Images: new Dictionary<string, byte[]> { ["images/t1.jpg"] = new byte[] { 0xFF, 0xD8 } });

      public Task<string> SubmitAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
      {
          return Task.FromResult("fake-angineer-job-1");
      }

      public Task<AnGineerJobState> GetStateAsync(string jobId, CancellationToken cancellationToken = default)
      {
          if (FailWith != null)
          {
              return Task.FromResult(AnGineerJobState.Failed);
          }
          if (StateSequence is { Count: > 0 })
          {
              return Task.FromResult(StateSequence.Dequeue());
          }
          return Task.FromResult(AnGineerJobState.Succeeded);
      }

      public Task<AnGineerPackage> DownloadPackageAsync(string jobId, CancellationToken cancellationToken = default)
      {
          return Task.FromResult(Package);
      }
  }
  ```

  在 `BidCompareApplicationTestModule.ConfigureServices` 标注处追加一行：

  ```csharp
  context.Services.Replace(ServiceDescriptor.Singleton<IAnGineerClient, FakeAnGineerClient>());
  ```

  （同时在文件顶部 `using` 区追加 `using DredgeAI.BidCompare.AnGineer;`。）

- [ ] **Step 5: 创建 IR DTO（内部适配 IR 形态，v2 §2 字段语义）**

  创建 `src/DredgeAI.BidCompare.Application.Contracts/Ir/DocumentIrDtos.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;

  namespace DredgeAI.BidCompare.Ir;

  /// <summary>内部适配 IR 结构（camelCase 字段名；由 AnGineerIrMapper 按 v2 §2/§3 从 doc_blocks_graph 映射，前端画 bbox 用）。</summary>
  public class DocumentIrDto
  {
      public string SchemaVersion { get; set; } = default!;

      public string DocId { get; set; } = default!;

      public IrMetaDto Meta { get; set; } = default!;

      public List<IrPageDto> Pages { get; set; } = new();

      public List<IrOutlineNodeDto> Outline { get; set; } = new();

      public List<IrBlockDto> Blocks { get; set; } = new();
  }

  public class IrMetaDto
  {
      public string FileName { get; set; } = default!;

      public int PageCount { get; set; }

      public string? Author { get; set; }

      public string? CreatorTool { get; set; }

      public DateTime? CreatedAt { get; set; }

      public DateTime? ModifiedAt { get; set; }
  }

  public class IrPageDto
  {
      public int PageIdx { get; set; }

      public double Width { get; set; }

      public double Height { get; set; }
  }

  public class IrOutlineNodeDto
  {
      public string Title { get; set; } = default!;

      public int Level { get; set; }

      public string? BlockId { get; set; }

      public List<IrOutlineNodeDto> Children { get; set; } = new();
  }

  public class IrBlockDto
  {
      public string BlockId { get; set; } = default!;

      public int PageIdx { get; set; }

      /// <summary>0~1 归一化坐标 [x0,y0,x1,y1]，左上角原点（v2 §2，前端 PDF_Viewer 直接还原）。</summary>
      public double[] Bbox { get; set; } = Array.Empty<double>();

      public string Type { get; set; } = default!;

      public string Text { get; set; } = default!;

      public int TextLevel { get; set; }

      /// <summary>v2 §4：AnGIneer 补齐前允许 null（OCR 降权随之降级关闭）。</summary>
      public string? Source { get; set; }

      /// <summary>v2 §4：允许 null；存在时 native 恒 1.0。</summary>
      public double? Confidence { get; set; }

      public IrTableDto? Table { get; set; }

      public string? ImgPath { get; set; }
  }

  public class IrTableDto
  {
      public string Html { get; set; } = default!;

      public string ImgPath { get; set; } = default!;
  }
  ```

- [ ] **Step 6: 写 ParseDocumentJob 失败测试（成功落库 + 失败降级 Partial + 校验拒收）**

  创建 `test/DredgeAI.BidCompare.Application.Tests/BackgroundJobs/ParseDocumentJobTests.cs`：

  ```csharp
  using System;
  using System.IO;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.CompareTasks;
  using DredgeAI.BidCompare.Documents;
  using DredgeAI.BidCompare.AnGineer;
  using DredgeAI.BidCompare.Storage;
  using Shouldly;
  using Volo.Abp.BackgroundJobs;
  using Xunit;

  namespace DredgeAI.BidCompare.BackgroundJobs;

  public class ParseDocumentJobTests : BidCompareApplicationTestBase
  {
      private readonly ICompareTaskAppService _appService;
      private readonly RecordingBackgroundJobManager _jobManager;
      private readonly InMemoryFileStorage _fileStorage;
      private readonly FakeAnGineerClient _anGineerClient;

      public ParseDocumentJobTests()
      {
          _appService = GetRequiredService<ICompareTaskAppService>();
          _jobManager = (RecordingBackgroundJobManager)GetRequiredService<IBackgroundJobManager>();
          _fileStorage = (InMemoryFileStorage)GetRequiredService<IFileStorage>();
          _anGineerClient = (FakeAnGineerClient)GetRequiredService<IAnGineerClient>();
      }

      private async Task<(CompareTaskDto Task, CompareDocumentDto Doc)> CreateTaskWithBidDocAsync(
          string fileName = "标书A.pdf")
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
          var doc = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, fileName,
              new MemoryStream(Encoding.UTF8.GetBytes("%PDF")));
          return (task, doc);
      }

      private async Task RunParseJobAsync(Guid taskId, Guid documentId)
      {
          var job = GetRequiredService<ParseDocumentJob>();
          await job.ExecuteAsync(new ParseDocumentArgs { TaskId = taskId, DocumentId = documentId });
      }

      [Fact]
      public async Task Successful_Parse_Should_Store_Ir_Package_And_Advance_State()
      {
          var (task, doc) = await CreateTaskWithBidDocAsync();
          _jobManager.Clear();

          await RunParseJobAsync(task.Id, doc.Id);

          var detail = await _appService.GetAsync(task.Id);
          detail.Status.ShouldBe(CompareTaskStatus.Comparing); // 无招标文件 → 直接进入比对
          _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/ir.json"); // 内部适配 IR（v2 映射后）
          _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/content.md");
          _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/images/t1.jpg");
          _fileStorage.Objects.Keys.ShouldContain($"compare/{task.Id}/{doc.Id}/raw/doc_blocks_graph.jsonl"); // AnGIneer 原始产物留档
          _jobManager.LastEnqueued<CompareDocumentsArgs>().ShouldNotBeNull();

          // IR API 可读取（spec §6 GET ir；内容为内部适配形态）
          var ir = await _appService.GetDocumentIrAsync(task.Id, doc.Id);
          ir.DocId.ShouldBe(doc.Id.ToString()); // 内部适配 IR 的 docId 为本系统文档 id
          ir.Meta.FileName.ShouldBe("标书A.pdf");
          ir.Blocks.Count.ShouldBe(3);
          ir.Blocks[1].Table.ShouldNotBeNull();
          ir.Blocks[0].Bbox.ShouldBe(new double[] { 0.0672, 0.0594, 0.9244, 0.095 }); // 0~1 归一化（v2 §2）
      }

      [Fact]
      public async Task Task_With_TenderDoc_Should_Wait_For_Clause_Confirmation()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
          var tender = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Tender, "招标文件.pdf",
              new MemoryStream(new byte[] { 1 }));
          var bid = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf",
              new MemoryStream(new byte[] { 1 }));

          await RunParseJobAsync(task.Id, tender.Id);
          await RunParseJobAsync(task.Id, bid.Id);

          var detail = await _appService.GetAsync(task.Id);
          detail.Status.ShouldBe(CompareTaskStatus.AwaitingClauses); // spec §5 步骤3：待条款确认
      }

      [Fact]
      public async Task AnGIneer_Failure_Should_Mark_Document_Failed_And_Task_Failed_When_All_Fail()
      {
          _anGineerClient.FailWith = "服务不可用";
          var (task, doc) = await CreateTaskWithBidDocAsync();

          await RunParseJobAsync(task.Id, doc.Id);

          var detail = await _appService.GetAsync(task.Id);
          detail.Status.ShouldBe(CompareTaskStatus.Failed); // spec §9：不静默降级，明确提示

          var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
          var failed = await docRepo.GetAsync(doc.Id);
          failed.ParseStatus.ShouldBe(DocumentParseStatus.Failed);
          failed.ParseError.ShouldNotBeNullOrWhiteSpace();
      }

      [Fact]
      public async Task Partial_Failure_Should_Mark_Partial_But_Continue()
      {
          // spec §9：单份解析失败 → 部分完成，其余文档照常对比
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
          var good = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf",
              new MemoryStream(new byte[] { 1 }));
          var bad = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf",
              new MemoryStream(new byte[] { 1 }));

          await RunParseJobAsync(task.Id, good.Id);

          _anGineerClient.FailWith = "OCR 崩溃";
          await RunParseJobAsync(task.Id, bad.Id);

          var detail = await _appService.GetAsync(task.Id);
          detail.Status.ShouldBe(CompareTaskStatus.Comparing); // Partial 为中间标记态，继续流转
          _jobManager.LastEnqueued<CompareDocumentsArgs>().ShouldNotBeNull();
      }

      [Fact]
      public async Task Invalid_Ir_Should_Be_Rejected_With_Reason()
      {
          // 映射后块缺少 blockId（graph 行无 block_uid）→ 内部适配 IR 校验拒收
          _anGineerClient.Package = _anGineerClient.Package with
          {
              GraphJsonl = Encoding.UTF8.GetBytes("{\"block_type\":\"paragraph\",\"page_idx\":0,\"plain_text\":\"缺 id\",\"bbox\":[0.1,0.1,0.9,0.2]}")
          };
          var (task, doc) = await CreateTaskWithBidDocAsync();

          await RunParseJobAsync(task.Id, doc.Id);

          var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
          var failed = await docRepo.GetAsync(doc.Id);
          failed.ParseStatus.ShouldBe(DocumentParseStatus.Failed);
          failed.ParseError.ShouldContain("IrValidationFailed");
      }

      [Fact]
      public async Task GetDocumentIr_Should_Throw_When_Not_Parsed()
      {
          var (task, doc) = await CreateTaskWithBidDocAsync();

          var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(
              () => _appService.GetDocumentIrAsync(task.Id, doc.Id));
          ex.Code.ShouldBe(BidCompareErrorCodes.IrNotReady);
      }
  }
  ```

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter ParseDocumentJobTests
  ```

  预期：**编译失败**（`ParseDocumentJob` / `CompareDocumentsArgs` / `GetDocumentIrAsync` 不存在）。

- [ ] **Step 7: 实现 ParseDocumentJob + CompareDocumentsArgs 占位类型**

  `src/DredgeAI.BidCompare.Application/BackgroundJobs/CompareDocumentsArgs.cs`（Job 类在 Task 9 实现）：

  ```csharp
  using System;

  namespace DredgeAI.BidCompare.BackgroundJobs;

  public class CompareDocumentsArgs
  {
      public Guid TaskId { get; set; }
  }
  ```

  `src/DredgeAI.BidCompare.Application/BackgroundJobs/ParseDocumentJob.cs`：

  ```csharp
  using System;
  using System.IO;
  using System.Linq;
  using System.Text;
  using System.Text.Json;
  using System.Threading;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.CompareTasks;
  using DredgeAI.BidCompare.Documents;
  using DredgeAI.BidCompare.AnGineer;
  using DredgeAI.BidCompare.Storage;
  using Microsoft.Extensions.Logging;
  using Microsoft.Extensions.Options;
  using Volo.Abp;
  using Volo.Abp.BackgroundJobs;
  using Volo.Abp.DependencyInjection;
  using Volo.Abp.Domain.Repositories;
  using Volo.Abp.Linq;

  namespace DredgeAI.BidCompare.BackgroundJobs;

  /// <summary>
  /// 解析后台任务（spec §5 步骤2）：下载原始文件 → 提交 AnGIneer → 轮询 → 下载产物包 →
  /// AnGineerIrMapper 映射为内部适配 IR（v2 §2/§3）→ IR 校验（不合格拒收并报原因）→
  /// 产物落对象存储（原始产物留档 raw/ + ir.json + content.md + images/）→ 更新文档与任务状态。
  /// 失败策略（spec §9）：单份失败标记原因、任务降级 Partial，其余照常；全部失败 → Failed。
  /// </summary>
  public class ParseDocumentJob : AsyncBackgroundJob<ParseDocumentArgs>, ITransientDependency
  {
      private readonly IRepository<CompareDocument, Guid> _documentRepository;
      private readonly IRepository<CompareTask, Guid> _taskRepository;
      private readonly IFileStorage _fileStorage;
      private readonly IAnGineerClient _anGineerClient;
      private readonly IIrValidator _irValidator;
      private readonly IBackgroundJobManager _backgroundJobManager;
      private readonly IAsyncQueryableExecuter _asyncExecuter;
      private readonly AnGineerPollOptions _pollOptions;

      public ParseDocumentJob(
          IRepository<CompareDocument, Guid> documentRepository,
          IRepository<CompareTask, Guid> taskRepository,
          IFileStorage fileStorage,
          IAnGineerClient anGineerClient,
          IIrValidator irValidator,
          IBackgroundJobManager backgroundJobManager,
          IAsyncQueryableExecuter asyncExecuter,
          IOptions<AnGineerPollOptions> pollOptions)
      {
          _documentRepository = documentRepository;
          _taskRepository = taskRepository;
          _fileStorage = fileStorage;
          _anGineerClient = anGineerClient;
          _irValidator = irValidator;
          _backgroundJobManager = backgroundJobManager;
          _asyncExecuter = asyncExecuter;
          _pollOptions = pollOptions.Value;
      }

      public override async Task ExecuteAsync(ParseDocumentArgs args, CancellationToken cancellationToken = default)
      {
          var document = await _documentRepository.FindAsync(args.DocumentId, cancellationToken: cancellationToken);
          if (document == null)
          {
              Logger.LogWarning("CompareDocument {DocumentId} 不存在，跳过解析", args.DocumentId);
              return;
          }
          var task = await _taskRepository.GetAsync(args.TaskId, cancellationToken: cancellationToken);

          try
          {
              document.MarkParsing();
              await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);

              await using var origin = await _fileStorage.GetAsync(document.OriginStorageKey, cancellationToken);
              var anGineerJobId = await _anGineerClient.SubmitAsync(document.FileName, origin, cancellationToken);

              var state = await PollUntilFinishedAsync(anGineerJobId, cancellationToken);
              if (state == AnGineerJobState.Failed)
              {
                  throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
                      .WithData("fileName", document.FileName);
              }

              var package = await _anGineerClient.DownloadPackageAsync(anGineerJobId, cancellationToken);

              // v2：AnGIneer 产物（graph jsonl + meta）→ 内部适配 IR
              string irJson;
              try
              {
                  irJson = AnGineerIrMapper.MapToIrJson(
                      Encoding.UTF8.GetString(package.GraphJsonl),
                      Encoding.UTF8.GetString(package.MetaJson),
                      document.Id.ToString());
              }
              catch (Exception ex) when (ex is JsonException or InvalidOperationException)
              {
                  throw new BusinessException(BidCompareErrorCodes.IrValidationFailed)
                      .WithData("errors", $"AnGIneer 产物映射失败：{ex.Message}");
              }

              var validation = _irValidator.Validate(irJson);
              if (!validation.IsValid)
              {
                  throw new BusinessException(BidCompareErrorCodes.IrValidationFailed)
                      .WithData("errors", string.Join("；", validation.Errors));
              }

              var prefix = $"compare/{args.TaskId}/{args.DocumentId}";

              // AnGIneer 原始产物留档（追溯/调试，v2 §1 数据源原样保存）
              await _fileStorage.UploadAsync($"{prefix}/raw/doc_blocks_graph.jsonl", new MemoryStream(package.GraphJsonl), "application/x-ndjson", cancellationToken);
              await _fileStorage.UploadAsync($"{prefix}/raw/doc_blocks_graph_meta.json", new MemoryStream(package.MetaJson), "application/json", cancellationToken);

              var irKey = $"{prefix}/ir.json"; // 内部适配 IR（非跨系统交付物）
              await _fileStorage.UploadAsync(irKey, new MemoryStream(Encoding.UTF8.GetBytes(irJson)), "application/json", cancellationToken);

              string? docMdKey = null;
              if (package.ContentMd != null)
              {
                  docMdKey = $"{prefix}/content.md";
                  await _fileStorage.UploadAsync(docMdKey, new MemoryStream(package.ContentMd), "text/markdown", cancellationToken);
              }

              foreach (var (path, bytes) in package.Images)
              {
                  await _fileStorage.UploadAsync($"{prefix}/{path}", new MemoryStream(bytes), "application/octet-stream", cancellationToken);
              }

              using var irDocument = JsonDocument.Parse(irJson);
              var pageCount = irDocument.RootElement.GetProperty("meta").GetProperty("pageCount").GetInt32();
              var ocrRatio = IrValidator.CalculateOcrLowConfidenceRatio(irDocument.RootElement);

              document.MarkParsed(irKey, docMdKey, pageCount, ocrRatio);
              await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
          }
          catch (Exception ex) when (ex is not OperationCanceledException)
          {
              Logger.LogWarning(ex, "文档 {DocumentId} 解析失败", args.DocumentId);
              document.MarkParseFailed(ex is BusinessException be && be.Code != null
                  ? $"{be.Code}: {string.Join("；", be.Data.Keys.Cast<string>().Select(k => be.Data[k]))}"
                  : ex.Message);
              await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
          }

          await AdvanceTaskStateAsync(task, cancellationToken);
      }

      private async Task<AnGineerJobState> PollUntilFinishedAsync(string anGineerJobId, CancellationToken cancellationToken)
      {
          var deadline = DateTime.UtcNow + _pollOptions.Timeout;
          while (DateTime.UtcNow < deadline)
          {
              var state = await _anGineerClient.GetStateAsync(anGineerJobId, cancellationToken);
              if (state != AnGineerJobState.Processing)
              {
                  return state;
              }
              await Task.Delay(_pollOptions.PollInterval, cancellationToken);
          }
          throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
              .WithData("reason", "轮询超时");
      }

      /// <summary>spec §5 步骤2→3→4：全部文档落定后推进任务状态。</summary>
      private async Task AdvanceTaskStateAsync(CompareTask task, CancellationToken cancellationToken)
      {
          var queryable = await _documentRepository.GetQueryableAsync();
          var documents = await _asyncExecuter.ToListAsync(queryable.Where(d => d.TaskId == task.Id));

          if (documents.Any(d => d.ParseStatus is DocumentParseStatus.Pending or DocumentParseStatus.Parsing))
          {
              task.UpdateProgress("parsing", 10 + 20 * documents.Count(d => d.ParseStatus == DocumentParseStatus.Parsed) / Math.Max(documents.Count, 1), null);
              await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
              return;
          }

          var failed = documents.Where(d => d.ParseStatus == DocumentParseStatus.Failed).ToList();
          var parsed = documents.Where(d => d.ParseStatus == DocumentParseStatus.Parsed).ToList();

          if (parsed.Count == 0)
          {
              // spec §9：AnGIneer 不可用/全部失败 → 明确提示，不静默降级
              task.MarkFailed("全部文档解析失败：" + string.Join("；", failed.Select(f => $"{f.FileName}: {f.ParseError}")));
              await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
              return;
          }

          if (failed.Count > 0)
          {
              task.MarkPartial(string.Join("；", failed.Select(f => $"{f.FileName}: {f.ParseError}")));
          }
          else
          {
              task.MarkParsed();
          }

          if (task.TenderDocumentId.HasValue && task.ClauseSnapshotJson == null)
          {
              task.MarkAwaitingClauses();
              task.UpdateProgress("clauses", 40, "等待条款确认");
          }
          else
          {
              task.MarkComparing();
              task.UpdateProgress("comparing", 60, "两两比对中");
              await _backgroundJobManager.EnqueueAsync(new CompareDocumentsArgs { TaskId = task.Id });
          }

          await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
      }
  }
  ```

  `src/DredgeAI.BidCompare.Application/BackgroundJobs/AnGineerPollOptions.cs`（Application 层轮询参数，Host 配置节绑定；默认对测试友好）：

  ```csharp
  using System;

  namespace DredgeAI.BidCompare.BackgroundJobs;

  public class AnGineerPollOptions
  {
      public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

      public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
  }
  ```

  在 `BidCompareHttpApiHostModule.ConfigureServices` 末尾追加：

  ```csharp
  Configure<AnGineerPollOptions>(context.Configuration.GetSection("AnGIneer"));
  ```

  （`using DredgeAI.BidCompare.BackgroundJobs;` 一并追加。）

- [ ] **Step 8: 实现 GetDocumentIrAsync（AppService 接口/实现/Controller 各追加一处）**

  `ICompareTaskAppService` 追加方法签名（`using DredgeAI.BidCompare.Ir;`）：

  ```csharp
  Task<DocumentIrDto> GetDocumentIrAsync(Guid id, Guid docId);
  ```

  `CompareTaskAppService` 追加方法：

  ```csharp
  public async Task<DocumentIrDto> GetDocumentIrAsync(Guid id, Guid docId)
  {
      await _taskRepository.GetAsync(id); // 任务不存在 → 404
      var document = await _documentRepository.FirstOrDefaultAsync(d => d.TaskId == id && d.Id == docId);
      if (document == null)
      {
          throw new BusinessException(BidCompareErrorCodes.DocumentNotFound).WithData("docId", docId);
      }
      if (document.ParseStatus != DocumentParseStatus.Parsed || document.IrStorageKey == null)
      {
          throw new BusinessException(BidCompareErrorCodes.IrNotReady).WithData("docId", docId);
      }

      await using var stream = await _fileStorage.GetAsync(document.IrStorageKey);
      var ir = await JsonSerializer.DeserializeAsync<DocumentIrDto>(stream, SnapshotJsonOptions);
      return ir!;
  }
  ```

  （`using DredgeAI.BidCompare.Ir;` 一并追加。）

  `CompareTaskController` 追加 action：

  ```csharp
  /// <summary>GET /api/compare/tasks/{id}/ir/{docId} 某文档的 IR（前端对比视图画 bbox 用）</summary>
  [HttpGet("{id}/ir/{docId}")]
  public Task<Ir.DocumentIrDto> GetDocumentIrAsync(Guid id, Guid docId)
      => _appService.GetDocumentIrAsync(id, docId);
  ```

- [ ] **Step 9: 实现 HttpAnGineerClient（生产 adapter）**

  创建 `src/DredgeAI.BidCompare.HttpApi.Host/AnGineer/AnGineerOptions.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.AnGineer;

  public class AnGineerOptions
  {
      /// <summary>AnGIneer HTTP API 基地址，如 http://localhost:8800。</summary>
      public string BaseUrl { get; set; } = "http://localhost:8800";

      public string? ApiKey { get; set; }
  }
  ```

  创建 `src/DredgeAI.BidCompare.HttpApi.Host/AnGineer/HttpAnGineerClient.cs`：

  ```csharp
  using System.Collections.Generic;
  using System.IO;
  using System.IO.Compression;
  using System.Net.Http;
  using System.Net.Http.Headers;
  using System.Net.Http.Json;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.Extensions.Options;
  using Volo.Abp;
  using Volo.Abp.DependencyInjection;

  namespace DredgeAI.BidCompare.AnGineer;

  /// <summary>
  /// AnGIneer HTTP API adapter。约定提供方接口形态：
  ///   POST {BaseUrl}/api/parse          multipart 文件 → { "jobId": "..." }
  ///   GET  {BaseUrl}/api/parse/{jobId}  → { "state": "processing|succeeded|failed" }
  ///   GET  {BaseUrl}/api/parse/{jobId}/package → zip（doc_blocks_graph.jsonl + doc_blocks_graph_meta.json + content.md + images/）
  /// 形态变化（如改消息队列）只替换本类（spec §11 待决事项1）。
  /// </summary>
  public class HttpAnGineerClient : IAnGineerClient, ITransientDependency
  {
      private readonly IHttpClientFactory _httpClientFactory;
      private readonly AnGineerOptions _options;

      public HttpAnGineerClient(IHttpClientFactory httpClientFactory, IOptions<AnGineerOptions> options)
      {
          _httpClientFactory = httpClientFactory;
          _options = options.Value;
      }

      public async Task<string> SubmitAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
      {
          var client = CreateClient();
          using var form = new MultipartFormDataContent();
          using var fileContent = new StreamContent(content);
          fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
          form.Add(fileContent, "file", fileName);

          using var response = await client.PostAsync("/api/parse", form, cancellationToken);
          response.EnsureSuccessStatusCode();
          var payload = await response.Content.ReadFromJsonAsync<SubmitResponse>(cancellationToken: cancellationToken);
          return payload?.JobId
              ?? throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed).WithData("reason", "提交响应缺少 jobId");
      }

      public async Task<AnGineerJobState> GetStateAsync(string jobId, CancellationToken cancellationToken = default)
      {
          var client = CreateClient();
          var payload = await client.GetFromJsonAsync<StateResponse>($"/api/parse/{jobId}", cancellationToken);
          return payload?.State?.ToLowerInvariant() switch
          {
              "succeeded" => AnGineerJobState.Succeeded,
              "failed" => AnGineerJobState.Failed,
              _ => AnGineerJobState.Processing
          };
      }

      public async Task<AnGineerPackage> DownloadPackageAsync(string jobId, CancellationToken cancellationToken = default)
      {
          var client = CreateClient();
          await using var zipStream = await client.GetStreamAsync($"/api/parse/{jobId}/package", cancellationToken);
          using var buffer = new MemoryStream();
          await zipStream.CopyToAsync(buffer, cancellationToken);
          buffer.Position = 0;

          byte[]? graphJsonl = null;
          byte[]? metaJson = null;
          byte[]? contentMd = null;
          var images = new Dictionary<string, byte[]>();

          using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
          foreach (var entry in archive.Entries)
          {
              var name = entry.FullName.Replace('\\', '/');
              using var entryStream = entry.Open();
              using var entryBuffer = new MemoryStream();
              await entryStream.CopyToAsync(entryBuffer, cancellationToken);
              if (name.EndsWith("doc_blocks_graph.jsonl", System.StringComparison.OrdinalIgnoreCase))
              {
                  graphJsonl = entryBuffer.ToArray();
              }
              else if (name.EndsWith("doc_blocks_graph_meta.json", System.StringComparison.OrdinalIgnoreCase))
              {
                  metaJson = entryBuffer.ToArray();
              }
              else if (name.EndsWith("content.md", System.StringComparison.OrdinalIgnoreCase))
              {
                  contentMd = entryBuffer.ToArray();
              }
              else if (name.StartsWith("images/", System.StringComparison.OrdinalIgnoreCase) && entry.Length > 0)
              {
                  images[name] = entryBuffer.ToArray();
              }
          }

          if (graphJsonl == null || metaJson == null)
          {
              throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
                  .WithData("reason", "产物包缺少 doc_blocks_graph.jsonl / doc_blocks_graph_meta.json");
          }
          return new AnGineerPackage(graphJsonl, metaJson, contentMd, images);
      }

      private HttpClient CreateClient()
      {
          var client = _httpClientFactory.CreateClient(nameof(HttpAnGineerClient));
          client.BaseAddress = new System.Uri(_options.BaseUrl.TrimEnd('/') + "/");
          if (!_options.ApiKey.IsNullOrWhiteSpace())
          {
              client.DefaultRequestHeaders.Authorization =
                  new AuthenticationHeaderValue("Bearer", _options.ApiKey);
          }
          return client;
      }

      private class SubmitResponse
      {
          public string? JobId { get; set; }
      }

      private class StateResponse
      {
          public string? State { get; set; }
      }
  }
  ```

  在 `BidCompareHttpApiHostModule.ConfigureServices` 末尾追加：

  ```csharp
  Configure<AnGineerOptions>(context.Configuration.GetSection("AnGIneer"));
  context.Services.AddHttpClient();
  ```

  `appsettings.json` 顶层追加：

  ```json
  "AnGIneer": {
    "BaseUrl": "http://localhost:8800",
    "ApiKey": null,
    "PollInterval": "00:00:05",
    "Timeout": "00:30:00"
  }
  ```

- [ ] **Step 10: 跑测试确认通过**

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter ParseDocumentJobTests
  dotnet test test/DredgeAI.BidCompare.Domain.Tests
  ```

  预期：ParseDocumentJobTests 6 passed；Domain.Tests 全绿。

- [ ] **Step 11: 提交**

  ```bash
  git add backend/DredgeAI.BidCompare
  git commit -m "feat(backend): add AnGIneer adapter, IR mapping/validation and parse background job with GET ir API"
  ```

---

## Task 9 【P1】算法服务对接：比对后台任务、证据持久化、证据查询与相似度矩阵 API

覆盖 spec §6 路由：`GET /api/compare/tasks/{id}/evidences`、`GET /api/compare/tasks/{id}/matrix`。算法服务调用契约：`POST /analyze/similarity|pricing|metadata`，请求为多份内部适配 IR（v2 映射后形态，bbox 0~1 归一化），响应为 Evidence 数组（字段名逐字遵守 spec §6.1）。

**Files:**
- Create: `src/DredgeAI.BidCompare.Domain/Analysis/ICompareAlgoClient.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/Evidences/EvidenceDto.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/Evidences/EvidenceLocationDto.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/Evidences/EvidenceMetricsDto.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/Evidences/GetEvidenceListInput.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/Analysis/SimilarityMatrixDto.cs`
- Create: `src/DredgeAI.BidCompare.Application/Evidences/EvidenceMapper.cs`
- Create: `src/DredgeAI.BidCompare.Application/BackgroundJobs/CompareDocumentsJob.cs`
- Modify: `src/DredgeAI.BidCompare.Application.Contracts/CompareTasks/ICompareTaskAppService.cs`
- Modify: `src/DredgeAI.BidCompare.Application/CompareTasks/CompareTaskAppService.cs`
- Modify: `src/DredgeAI.BidCompare.HttpApi/Controllers/CompareTaskController.cs`
- Create: `src/DredgeAI.BidCompare.HttpApi.Host/Analysis/AlgoServiceOptions.cs`
- Create: `src/DredgeAI.BidCompare.HttpApi.Host/Analysis/HttpCompareAlgoClient.cs`
- Modify: `src/DredgeAI.BidCompare.HttpApi.Host/BidCompareHttpApiHostModule.cs`
- Modify: `src/DredgeAI.BidCompare.HttpApi.Host/appsettings.json`
- Test: `test/DredgeAI.BidCompare.TestBase/Fakes/FakeCompareAlgoClient.cs`
- Modify: `test/DredgeAI.BidCompare.Application.Tests/BidCompareApplicationTestModule.cs`
- Test: `test/DredgeAI.BidCompare.Application.Tests/BackgroundJobs/CompareDocumentsJobTests.cs`

**Steps:**

- [ ] **Step 1: 写失败测试（证据落库 → 查询过滤 → 矩阵 → 算法服务不可用降级 Failed）**

  创建 `test/DredgeAI.BidCompare.TestBase/Fakes/FakeCompareAlgoClient.cs`：

  ```csharp
  using System.Collections.Generic;
  using System.Threading;
  using System.Threading.Tasks;

  namespace DredgeAI.BidCompare.Analysis;

  /// <summary>可编程 Fake：按端点预设响应；FailWith 非空时全部抛 HttpRequestException 模拟服务不可用。</summary>
  public class FakeCompareAlgoClient : ICompareAlgoClient
  {
      public List<AlgoEvidence> SimilarityEvidences { get; set; } = new();

      public List<AlgoEvidence> PricingEvidences { get; set; } = new();

      public List<AlgoEvidence> MetadataEvidences { get; set; } = new();

      public string? FailWith { get; set; }

      public IReadOnlyList<AlgoIrDocument>? LastRequest { get; private set; }

      public Task<IReadOnlyList<AlgoEvidence>> AnalyzeSimilarityAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default)
      {
          LastRequest = documents;
          return Respond(SimilarityEvidences);
      }

      public Task<IReadOnlyList<AlgoEvidence>> AnalyzePricingAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default)
      {
          LastRequest = documents;
          return Respond(PricingEvidences);
      }

      public Task<IReadOnlyList<AlgoEvidence>> AnalyzeMetadataAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default)
      {
          LastRequest = documents;
          return Respond(MetadataEvidences);
      }

      private Task<IReadOnlyList<AlgoEvidence>> Respond(List<AlgoEvidence> evidences)
      {
          if (FailWith != null)
          {
              throw new System.Net.Http.HttpRequestException(FailWith);
          }
          return Task.FromResult<IReadOnlyList<AlgoEvidence>>(evidences);
      }
  }
  ```

  在 `BidCompareApplicationTestModule.ConfigureServices` 标注处追加（`using DredgeAI.BidCompare.Analysis;` 一并追加）：

  ```csharp
  context.Services.Replace(ServiceDescriptor.Singleton<ICompareAlgoClient, FakeCompareAlgoClient>());
  ```

  创建 `test/DredgeAI.BidCompare.Application.Tests/BackgroundJobs/CompareDocumentsJobTests.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text;
  using System.Text.Json;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.Analysis;
  using DredgeAI.BidCompare.CompareTasks;
  using DredgeAI.BidCompare.Documents;
  using DredgeAI.BidCompare.Evidences;
  using Shouldly;
  using Xunit;

  namespace DredgeAI.BidCompare.BackgroundJobs;

  public class CompareDocumentsJobTests : BidCompareApplicationTestBase
  {
      private readonly ICompareTaskAppService _appService;
      private readonly FakeCompareAlgoClient _algoClient;

      public CompareDocumentsJobTests()
      {
          _appService = GetRequiredService<ICompareTaskAppService>();
          _algoClient = (FakeCompareAlgoClient)GetRequiredService<ICompareAlgoClient>();
      }

      /// <summary>建 2 份标书并跑完解析，返回 (taskId, docAId, docBId)。</summary>
      private async Task<(Guid TaskId, Guid DocA, Guid DocB)> PrepareParsedTaskAsync()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
          var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", new MemoryStream(new byte[] { 1 }));
          var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf", new MemoryStream(new byte[] { 2 }));
          var parseJob = GetRequiredService<ParseDocumentJob>();
          await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docA.Id });
          await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docB.Id });
          return (task.Id, docA.Id, docB.Id);
      }

      private void SetupAlgoEvidences(Guid docA, Guid docB)
      {
          _algoClient.SimilarityEvidences = new List<AlgoEvidence>
          {
              new()
              {
                  Type = "similarity",
                  Severity = "high",
                  DocIds = new List<string> { docA.ToString(), docB.ToString() },
                  Locations = new List<AlgoEvidenceLocation>
                  {
                      new() { DocId = docA.ToString(), BlockIds = new List<string> { "b0001" } },
                      new() { DocId = docB.ToString(), BlockIds = new List<string> { "b0001" } }
                  },
                  Metrics = new Dictionary<string, JsonElement>
                  {
                      ["similarity"] = JsonDocument.Parse("0.93").RootElement.Clone()
                  },
                  Title = "标书A与标书B大段雷同",
                  Description = "第三章相似度 0.93"
              }
          };
          _algoClient.PricingEvidences = new List<AlgoEvidence>
          {
              new()
              {
                  Type = "pricing",
                  Severity = "mid",
                  DocIds = new List<string> { docA.ToString(), docB.ToString() },
                  Locations = new List<AlgoEvidenceLocation>(),
                  Metrics = new Dictionary<string, JsonElement>(),
                  Title = "报价呈等差规律",
                  Description = "两份报价差值固定 1000 元"
              }
          };
      }

      [Fact]
      public async Task Compare_Job_Should_Persist_Evidences_And_Finish_Task()
      {
          var (taskId, docA, docB) = await PrepareParsedTaskAsync();
          SetupAlgoEvidences(docA, docB);

          var job = GetRequiredService<CompareDocumentsJob>();
          await job.ExecuteAsync(new CompareDocumentsArgs { TaskId = taskId });

          var detail = await _appService.GetAsync(taskId);
          detail.Status.ShouldBe(CompareTaskStatus.Done); // P1 无 AI 阶段，比对完成即 Done
          detail.Progress.Percent.ShouldBe(100);

          var list = await _appService.GetEvidencesAsync(taskId, new GetEvidenceListInput { MaxResultCount = 10 });
          list.TotalCount.ShouldBe(2);
          list.Items.ShouldAllBe(e => e.AiGenerated == false);

          var similarity = list.Items.Single(e => e.Type == EvidenceType.Similarity);
          similarity.Severity.ShouldBe(EvidenceSeverity.High);
          similarity.DocIds.ShouldBe(new[] { docA, docB }, ignoreOrder: true);
          similarity.Locations.Count.ShouldBe(2);
          similarity.Locations[0].BlockIds.ShouldContain("b0001");
          similarity.Metrics.ShouldNotBeNull();
          similarity.Metrics!.Similarity.ShouldBe(0.93);
          similarity.Title.ShouldBe("标书A与标书B大段雷同");
      }

      [Fact]
      public async Task Evidences_Should_Filter_By_Type_Severity_And_DocPair()
      {
          var (taskId, docA, docB) = await PrepareParsedTaskAsync();
          SetupAlgoEvidences(docA, docB);
          await GetRequiredService<CompareDocumentsJob>().ExecuteAsync(new CompareDocumentsArgs { TaskId = taskId });

          var byType = await _appService.GetEvidencesAsync(taskId,
              new GetEvidenceListInput { Type = EvidenceType.Pricing, MaxResultCount = 10 });
          byType.TotalCount.ShouldBe(1);

          var bySeverity = await _appService.GetEvidencesAsync(taskId,
              new GetEvidenceListInput { Severity = EvidenceSeverity.High, MaxResultCount = 10 });
          bySeverity.TotalCount.ShouldBe(1);

          var byPair = await _appService.GetEvidencesAsync(taskId,
              new GetEvidenceListInput { DocIdA = docA, DocIdB = docB, MaxResultCount = 10 });
          byPair.TotalCount.ShouldBe(2);

          var byPairMiss = await _appService.GetEvidencesAsync(taskId,
              new GetEvidenceListInput { DocIdA = docA, DocIdB = Guid.NewGuid(), MaxResultCount = 10 });
          byPairMiss.TotalCount.ShouldBe(0);
      }

      [Fact]
      public async Task Matrix_Should_Be_NxN_With_Diagonal_One()
      {
          var (taskId, docA, docB) = await PrepareParsedTaskAsync();
          SetupAlgoEvidences(docA, docB);
          await GetRequiredService<CompareDocumentsJob>().ExecuteAsync(new CompareDocumentsArgs { TaskId = taskId });

          var matrix = await _appService.GetMatrixAsync(taskId);

          matrix.DocIds.ShouldBe(new[] { docA, docB });
          matrix.Cells.Count.ShouldBe(4); // N×N = 2×2
          matrix.Cells.Single(c => c.DocAId == docA && c.DocBId == docA).Similarity.ShouldBe(1.0);
          matrix.Cells.Single(c => c.DocAId == docA && c.DocBId == docB).Similarity.ShouldBe(0.93);
          matrix.Cells.Single(c => c.DocAId == docB && c.DocBId == docA).Similarity.ShouldBe(0.93);
      }

      [Fact]
      public async Task Algo_Service_Unavailable_Should_Mark_Task_Failed()
      {
          // spec §9：不静默降级，明确提示
          var (taskId, _, _) = await PrepareParsedTaskAsync();
          _algoClient.FailWith = "connection refused";

          await GetRequiredService<CompareDocumentsJob>().ExecuteAsync(new CompareDocumentsArgs { TaskId = taskId });

          var detail = await _appService.GetAsync(taskId);
          detail.Status.ShouldBe(CompareTaskStatus.Failed);
      }

      [Fact]
      public async Task Less_Than_Two_Parsed_Bids_Should_Fail_Task()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
          var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", new MemoryStream(new byte[] { 1 }));
          await GetRequiredService<ParseDocumentJob>().ExecuteAsync(
              new ParseDocumentArgs { TaskId = task.Id, DocumentId = docA.Id });
          // 只有 1 份解析成功（手动触发比对，模拟边界）
          await GetRequiredService<CompareDocumentsJob>().ExecuteAsync(new CompareDocumentsArgs { TaskId = task.Id });

          var detail = await _appService.GetAsync(task.Id);
          detail.Status.ShouldBe(CompareTaskStatus.Failed);
      }
  }
  ```

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter CompareDocumentsJobTests
  ```

  预期：**编译失败**（`ICompareAlgoClient`/`CompareDocumentsJob`/证据 DTO 不存在）。

- [ ] **Step 2: 定义 ICompareAlgoClient（Domain）**

  创建 `src/DredgeAI.BidCompare.Domain/Analysis/ICompareAlgoClient.cs`：

  ```csharp
  using System.Collections.Generic;
  using System.Text.Json;
  using System.Threading;
  using System.Threading.Tasks;

  namespace DredgeAI.BidCompare.Analysis;

  /// <summary>发送给算法服务的单份内部适配 IR（docId 为本系统文档 Guid 字符串；IrJson 为 v2 映射后形态，bbox 0~1 归一化；DocMd 为 content.md 内容）。</summary>
  public record AlgoIrDocument(string DocId, string IrJson, string? DocMd);

  /// <summary>
  /// 算法服务返回的证据项（spec §6.1 Evidence 子集，aiGenerated 恒为 false 由本服务补充）。
  /// JSON 字段名逐字遵守：type/severity/docIds/locations/docId/blockIds/metrics/title/description。
  /// </summary>
  public class AlgoEvidence
  {
      public string Type { get; set; } = default!;

      public string Severity { get; set; } = default!;

      public List<string> DocIds { get; set; } = new();

      public List<AlgoEvidenceLocation> Locations { get; set; } = new();

      public Dictionary<string, JsonElement>? Metrics { get; set; }

      public string Title { get; set; } = default!;

      public string Description { get; set; } = default!;
  }

  public class AlgoEvidenceLocation
  {
      public string DocId { get; set; } = default!;

      public List<string> BlockIds { get; set; } = new();
  }

  /// <summary>
  /// Python 算法服务 client（spec §3.1 compare-algo：纯确定性，输入 IR 输出结构化证据项）。
  /// 三个端点：POST /analyze/similarity、/analyze/pricing、/analyze/metadata。
  /// </summary>
  public interface ICompareAlgoClient
  {
      Task<IReadOnlyList<AlgoEvidence>> AnalyzeSimilarityAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default);

      Task<IReadOnlyList<AlgoEvidence>> AnalyzePricingAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default);

      Task<IReadOnlyList<AlgoEvidence>> AnalyzeMetadataAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default);
  }
  ```

- [ ] **Step 3: 创建证据/矩阵 DTO（spec §6.1 字段名逐字遵守）**

  `Evidences/EvidenceDto.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;
  using Volo.Abp.Application.Dtos;

  namespace DredgeAI.BidCompare.Evidences;

  /// <summary>spec §6.1 Evidence：id, taskId, type, severity, docIds, locations, metrics, title, description, aiGenerated。</summary>
  public class EvidenceDto : EntityDto<Guid>
  {
      public Guid TaskId { get; set; }

      public EvidenceType Type { get; set; }

      public EvidenceSeverity Severity { get; set; }

      public List<Guid> DocIds { get; set; } = new();

      public List<EvidenceLocationDto> Locations { get; set; } = new();

      public EvidenceMetricsDto? Metrics { get; set; }

      public string Title { get; set; } = default!;

      public string Description { get; set; } = default!;

      public bool AiGenerated { get; set; }
  }
  ```

  `Evidences/EvidenceLocationDto.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;

  namespace DredgeAI.BidCompare.Evidences;

  /// <summary>spec §6.1 locations: { docId, blockIds[] }[]。</summary>
  public class EvidenceLocationDto
  {
      public Guid DocId { get; set; }

      public List<string> BlockIds { get; set; } = new();
  }
  ```

  `Evidences/EvidenceMetricsDto.cs`：

  ```csharp
  using System.Collections.Generic;
  using System.Text.Json;
  using System.Text.Json.Serialization;

  namespace DredgeAI.BidCompare.Evidences;

  /// <summary>spec §6.1 metrics: { similarity? }；JsonExtensionData 透传算法服务后续扩展指标。</summary>
  public class EvidenceMetricsDto
  {
      public double? Similarity { get; set; }

      [JsonExtensionData]
      public Dictionary<string, JsonElement>? Extra { get; set; }
  }
  ```

  `Evidences/GetEvidenceListInput.cs`：

  ```csharp
  using System;
  using Volo.Abp.Application.Dtos;

  namespace DredgeAI.BidCompare.Evidences;

  /// <summary>spec §6：按类型/严重度/文档对过滤。</summary>
  public class GetEvidenceListInput : PagedResultRequestDto
  {
      public EvidenceType? Type { get; set; }

      public EvidenceSeverity? Severity { get; set; }

      public Guid? DocIdA { get; set; }

      public Guid? DocIdB { get; set; }
  }
  ```

  `Analysis/SimilarityMatrixDto.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;

  namespace DredgeAI.BidCompare.Analysis;

  /// <summary>spec §6：两两相似度矩阵（N×N，热力图用）。DocIds 定序，Cells 为 N×N 全量（对角线 1.0）。</summary>
  public class SimilarityMatrixDto
  {
      public List<Guid> DocIds { get; set; } = new();

      public List<SimilarityMatrixCellDto> Cells { get; set; } = new();
  }

  public class SimilarityMatrixCellDto
  {
      public Guid DocAId { get; set; }

      public Guid DocBId { get; set; }

      public double Similarity { get; set; }
  }
  ```

- [ ] **Step 4: 实现 EvidenceMapper 与 CompareDocumentsJob**

  `src/DredgeAI.BidCompare.Application/Evidences/EvidenceMapper.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text.Json;
  using DredgeAI.BidCompare.Analysis;

  namespace DredgeAI.BidCompare.Evidences;

  /// <summary>EvidenceItem 实体 ⇄ DTO / AlgoEvidence 转换（JSON 负载 camelCase，与 spec §6.1 一致）。</summary>
  public static class EvidenceMapper
  {
      private static readonly JsonSerializerOptions JsonOptions = new()
      {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          PropertyNameCaseInsensitive = true
      };

      public static EvidenceDto ToDto(EvidenceItem entity)
      {
          return new EvidenceDto
          {
              Id = entity.Id,
              TaskId = entity.TaskId,
              Type = entity.Type,
              Severity = entity.Severity,
              DocIds = JsonSerializer.Deserialize<List<Guid>>(entity.DocIdsJson, JsonOptions) ?? new(),
              Locations = JsonSerializer.Deserialize<List<EvidenceLocationDto>>(entity.LocationsJson, JsonOptions) ?? new(),
              Metrics = entity.MetricsJson == null
                  ? null
                  : JsonSerializer.Deserialize<EvidenceMetricsDto>(entity.MetricsJson, JsonOptions),
              Title = entity.Title,
              Description = entity.Description,
              AiGenerated = entity.AiGenerated
          };
      }

      public static EvidenceItem ToEntity(Guid id, Guid taskId, AlgoEvidence algo)
      {
          var docIds = algo.DocIds.Select(Guid.Parse).ToList();
          var locations = algo.Locations.Select(l => new EvidenceLocationDto
          {
              DocId = Guid.Parse(l.DocId),
              BlockIds = l.BlockIds
          }).ToList();

          return new EvidenceItem(
              id,
              taskId,
              ParseEnum<EvidenceType>(algo.Type, EvidenceType.Metadata),
              ParseEnum<EvidenceSeverity>(algo.Severity, EvidenceSeverity.Low),
              JsonSerializer.Serialize(docIds, JsonOptions),
              JsonSerializer.Serialize(locations, JsonOptions),
              algo.Metrics == null ? null : JsonSerializer.Serialize(algo.Metrics, JsonOptions),
              algo.Title,
              algo.Description,
              aiGenerated: false);
      }

      public static string SerializeDocIds(IEnumerable<Guid> docIds)
          => JsonSerializer.Serialize(docIds.ToList(), JsonOptions);

      public static string SerializeLocations(IEnumerable<EvidenceLocationDto> locations)
          => JsonSerializer.Serialize(locations.ToList(), JsonOptions);

      public static List<Guid> DeserializeDocIds(string json)
          => JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? new();

      public static double? ReadSimilarity(string? metricsJson)
      {
          if (metricsJson == null)
          {
              return null;
          }
          var metrics = JsonSerializer.Deserialize<EvidenceMetricsDto>(metricsJson, JsonOptions);
          return metrics?.Similarity;
      }

      private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
          => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
  }
  ```

  `src/DredgeAI.BidCompare.Application/BackgroundJobs/CompareDocumentsJob.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text;
  using System.Threading;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.Analysis;
  using DredgeAI.BidCompare.CompareTasks;
  using DredgeAI.BidCompare.Documents;
  using DredgeAI.BidCompare.Evidences;
  using DredgeAI.BidCompare.Storage;
  using Microsoft.Extensions.Logging;
  using Volo.Abp.BackgroundJobs;
  using Volo.Abp.DependencyInjection;
  using Volo.Abp.Domain.Repositories;
  using Volo.Abp.Guids;
  using Volo.Abp.Linq;

  namespace DredgeAI.BidCompare.BackgroundJobs;

  /// <summary>
  /// 比对后台任务（spec §5 步骤4）：汇总已解析标书 IR → 调算法服务三个端点 → 证据落库。
  /// 算法服务不可用 → 任务 Failed（spec §9：不静默降级）。
  /// P1 版本比对完成即 Done；Task 12（P2）会把尾部改为 MarkAnalyzing + 入队 AiAnalysisJob。
  /// </summary>
  public class CompareDocumentsJob : AsyncBackgroundJob<CompareDocumentsArgs>, ITransientDependency
  {
      private readonly IRepository<CompareTask, Guid> _taskRepository;
      private readonly IRepository<CompareDocument, Guid> _documentRepository;
      private readonly IRepository<EvidenceItem, Guid> _evidenceRepository;
      private readonly IFileStorage _fileStorage;
      private readonly ICompareAlgoClient _algoClient;
      private readonly IAsyncQueryableExecuter _asyncExecuter;
      private readonly IGuidGenerator _guidGenerator;

      public CompareDocumentsJob(
          IRepository<CompareTask, Guid> taskRepository,
          IRepository<CompareDocument, Guid> documentRepository,
          IRepository<EvidenceItem, Guid> evidenceRepository,
          IFileStorage fileStorage,
          ICompareAlgoClient algoClient,
          IAsyncQueryableExecuter asyncExecuter,
          IGuidGenerator guidGenerator)
      {
          _taskRepository = taskRepository;
          _documentRepository = documentRepository;
          _evidenceRepository = evidenceRepository;
          _fileStorage = fileStorage;
          _algoClient = algoClient;
          _asyncExecuter = asyncExecuter;
          _guidGenerator = guidGenerator;
      }

      public override async Task ExecuteAsync(CompareDocumentsArgs args, CancellationToken cancellationToken = default)
      {
          var task = await _taskRepository.GetAsync(args.TaskId, cancellationToken: cancellationToken);

          var queryable = await _documentRepository.GetQueryableAsync();
          var bidDocs = await _asyncExecuter.ToListAsync(queryable.Where(d =>
              d.TaskId == args.TaskId &&
              d.Role == DocumentRole.Bid &&
              d.ParseStatus == DocumentParseStatus.Parsed));

          if (bidDocs.Count < 2)
          {
              task.MarkFailed($"可比对标书不足 2 份（当前 {bidDocs.Count} 份）");
              await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
              return;
          }

          var algoDocuments = new List<AlgoIrDocument>();
          foreach (var doc in bidDocs)
          {
              await using var irStream = await _fileStorage.GetAsync(doc.IrStorageKey!, cancellationToken);
              var irJson = await ReadAllAsync(irStream, cancellationToken);
              string? docMd = null;
              if (doc.DocMdStorageKey != null)
              {
                  await using var mdStream = await _fileStorage.GetAsync(doc.DocMdStorageKey, cancellationToken);
                  docMd = await ReadAllAsync(mdStream, cancellationToken);
              }
              algoDocuments.Add(new AlgoIrDocument(doc.Id.ToString(), irJson, docMd));
          }

          List<AlgoEvidence> algoEvidences;
          try
          {
              algoEvidences = (await _algoClient.AnalyzeSimilarityAsync(algoDocuments, cancellationToken))
                  .Concat(await _algoClient.AnalyzePricingAsync(algoDocuments, cancellationToken))
                  .Concat(await _algoClient.AnalyzeMetadataAsync(algoDocuments, cancellationToken))
                  .ToList();
          }
          catch (Exception ex) when (ex is not OperationCanceledException)
          {
              Logger.LogWarning(ex, "算法服务调用失败，任务 {TaskId} 标记 Failed", args.TaskId);
              task.MarkFailed($"算法服务不可用：{ex.Message}");
              await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
              return;
          }

          foreach (var algoEvidence in algoEvidences)
          {
              await _evidenceRepository.InsertAsync(
                  EvidenceMapper.ToEntity(_guidGenerator.Create(), args.TaskId, algoEvidence),
                  cancellationToken: cancellationToken);
          }

          task.MarkAnalyzing();
          task.MarkDone();
          task.UpdateProgress("done", 100, null);
          await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
      }

      private static async Task<string> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
      {
          using var buffer = new MemoryStream();
          await stream.CopyToAsync(buffer, cancellationToken);
          return Encoding.UTF8.GetString(buffer.ToArray());
      }
  }
  ```

- [ ] **Step 5: AppService 追加 GetEvidencesAsync / GetMatrixAsync + Controller 追加 action**

  `ICompareTaskAppService` 追加（`using DredgeAI.BidCompare.Analysis;`、`using DredgeAI.BidCompare.Evidences;`）：

  ```csharp
  Task<PagedResultDto<EvidenceDto>> GetEvidencesAsync(Guid id, GetEvidenceListInput input);

  Task<SimilarityMatrixDto> GetMatrixAsync(Guid id);
  ```

  `CompareTaskAppService` 追加（`using DredgeAI.BidCompare.Analysis;`）：

  ```csharp
  public async Task<PagedResultDto<EvidenceDto>> GetEvidencesAsync(Guid id, GetEvidenceListInput input)
  {
      await _taskRepository.GetAsync(id);

      var queryable = await _evidenceRepository.GetQueryableAsync();
      queryable = queryable
          .Where(e => e.TaskId == id)
          .WhereIf(input.Type.HasValue, e => e.Type == input.Type!.Value)
          .WhereIf(input.Severity.HasValue, e => e.Severity == input.Severity!.Value);

      // 文档对过滤涉及 JSON 负载，原型规模（单任务证据量有限）在内存过滤后再分页
      var entities = await AsyncExecuter.ToListAsync(queryable.OrderBy(e => e.Severity).ThenBy(e => e.CreationTime));
      var dtos = entities.Select(EvidenceMapper.ToDto).ToList();

      if (input.DocIdA.HasValue && input.DocIdB.HasValue)
      {
          dtos = dtos.Where(e => e.DocIds.Contains(input.DocIdA.Value) && e.DocIds.Contains(input.DocIdB.Value)).ToList();
      }

      return new PagedResultDto<EvidenceDto>(
          dtos.Count,
          dtos.Skip(input.SkipCount).Take(input.MaxResultCount).ToList());
  }

  public async Task<SimilarityMatrixDto> GetMatrixAsync(Guid id)
  {
      await _taskRepository.GetAsync(id);

      var docQueryable = await _documentRepository.GetQueryableAsync();
      var docs = await AsyncExecuter.ToListAsync(docQueryable
          .Where(d => d.TaskId == id && d.Role == DocumentRole.Bid && d.ParseStatus == DocumentParseStatus.Parsed)
          .OrderBy(d => d.CreationTime));

      var evQueryable = await _evidenceRepository.GetQueryableAsync();
      var similarityEvidences = await AsyncExecuter.ToListAsync(
          evQueryable.Where(e => e.TaskId == id && e.Type == EvidenceType.Similarity));

      var pairs = similarityEvidences
          .Select(e => (DocIds: EvidenceMapper.DeserializeDocIds(e.DocIdsJson),
                        Similarity: EvidenceMapper.ReadSimilarity(e.MetricsJson)))
          .ToList();

      var cells = new List<SimilarityMatrixCellDto>();
      foreach (var a in docs)
      {
          foreach (var b in docs)
          {
              var similarity = a.Id == b.Id
                  ? 1.0
                  : pairs.Where(p => p.Similarity.HasValue && p.DocIds.Contains(a.Id) && p.DocIds.Contains(b.Id))
                         .Select(p => p.Similarity!.Value)
                         .DefaultIfEmpty(0.0)
                         .Max();
              cells.Add(new SimilarityMatrixCellDto
              {
                  DocAId = a.Id,
                  DocBId = b.Id,
                  Similarity = Math.Round(similarity, 4)
              });
          }
      }

      return new SimilarityMatrixDto
      {
          DocIds = docs.Select(d => d.Id).ToList(),
          Cells = cells
      };
  }
  ```

  `CompareTaskController` 追加 action：

  ```csharp
  /// <summary>GET /api/compare/tasks/{id}/evidences 证据项列表（按类型/严重度/文档对过滤）</summary>
  [HttpGet("{id}/evidences")]
  public Task<PagedResultDto<Evidences.EvidenceDto>> GetEvidencesAsync(Guid id, [FromQuery] Evidences.GetEvidenceListInput input)
      => _appService.GetEvidencesAsync(id, input);

  /// <summary>GET /api/compare/tasks/{id}/matrix 两两相似度矩阵（N×N，热力图用）</summary>
  [HttpGet("{id}/matrix")]
  public Task<Analysis.SimilarityMatrixDto> GetMatrixAsync(Guid id)
      => _appService.GetMatrixAsync(id);
  ```

- [ ] **Step 6: 实现 HttpCompareAlgoClient（生产）**

  创建 `src/DredgeAI.BidCompare.HttpApi.Host/Analysis/AlgoServiceOptions.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.Analysis;

  public class AlgoServiceOptions
  {
      /// <summary>Python 算法服务基地址，如 http://localhost:8900。</summary>
      public string BaseUrl { get; set; } = "http://localhost:8900";

      /// <summary>单次请求超时（秒）。多份 100~500 页标书比对耗时长，默认 10 分钟。</summary>
      public int TimeoutSeconds { get; set; } = 600;
  }
  ```

  创建 `src/DredgeAI.BidCompare.HttpApi.Host/Analysis/HttpCompareAlgoClient.cs`：

  ```csharp
  using System.Collections.Generic;
  using System.Net.Http;
  using System.Net.Http.Json;
  using System.Text.Json;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.Extensions.Options;
  using Volo.Abp;
  using Volo.Abp.DependencyInjection;

  namespace DredgeAI.BidCompare.Analysis;

  /// <summary>算法服务 HTTP client：POST {BaseUrl}/analyze/similarity|pricing|metadata。</summary>
  public class HttpCompareAlgoClient : ICompareAlgoClient, ITransientDependency
  {
      private static readonly JsonSerializerOptions JsonOptions = new()
      {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          PropertyNameCaseInsensitive = true
      };

      private readonly IHttpClientFactory _httpClientFactory;
      private readonly AlgoServiceOptions _options;

      public HttpCompareAlgoClient(IHttpClientFactory httpClientFactory, IOptions<AlgoServiceOptions> options)
      {
          _httpClientFactory = httpClientFactory;
          _options = options.Value;
      }

      public Task<IReadOnlyList<AlgoEvidence>> AnalyzeSimilarityAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default)
          => PostAsync("/analyze/similarity", documents, cancellationToken);

      public Task<IReadOnlyList<AlgoEvidence>> AnalyzePricingAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default)
          => PostAsync("/analyze/pricing", documents, cancellationToken);

      public Task<IReadOnlyList<AlgoEvidence>> AnalyzeMetadataAsync(IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken = default)
          => PostAsync("/analyze/metadata", documents, cancellationToken);

      private async Task<IReadOnlyList<AlgoEvidence>> PostAsync(
          string path, IReadOnlyList<AlgoIrDocument> documents, CancellationToken cancellationToken)
      {
          var client = _httpClientFactory.CreateClient(nameof(HttpCompareAlgoClient));
          client.BaseAddress = new System.Uri(_options.BaseUrl.TrimEnd('/') + "/");
          client.Timeout = System.TimeSpan.FromSeconds(_options.TimeoutSeconds);

          using var response = await client.PostAsJsonAsync(
              path.TrimStart('/'),
              new { documents },
              JsonOptions,
              cancellationToken);
          response.EnsureSuccessStatusCode();

          var payload = await response.Content.ReadFromJsonAsync<AnalyzeResponse>(JsonOptions, cancellationToken);
          return payload?.Evidences
              ?? throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                  .WithData("reason", $"算法服务 {path} 响应缺少 evidences");
      }

      private class AnalyzeResponse
      {
          public List<AlgoEvidence>? Evidences { get; set; }
      }
  }
  ```

  在 `BidCompareHttpApiHostModule.ConfigureServices` 末尾追加：

  ```csharp
  Configure<AlgoServiceOptions>(context.Configuration.GetSection("AlgoService"));
  ```

  `appsettings.json` 顶层追加：

  ```json
  "AlgoService": {
    "BaseUrl": "http://localhost:8900",
    "TimeoutSeconds": 600
  }
  ```

- [ ] **Step 7: 跑测试确认通过**

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter CompareDocumentsJobTests
  ```

  预期：5 passed。

- [ ] **Step 8: 提交**

  ```bash
  git add backend/DredgeAI.BidCompare
  git commit -m "feat(backend): add algo service client, compare job, evidence list and similarity matrix APIs"
  ```

---

## Task 10 【P2】条款库 CRUD API

覆盖 spec §6 路由：`GET /api/compare/clause-templates`（分页）、`POST /api/compare/clause-templates`；补充路由 `PUT/DELETE /api/compare/clause-templates/{id}`（用户手动维护条款库需要完整 CRUD）。

**Files:**
- Create: `src/DredgeAI.BidCompare.Application.Contracts/ClauseTemplates/ClauseTemplateDto.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/ClauseTemplates/ClauseTemplateCreateUpdateDto.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/ClauseTemplates/GetClauseTemplatesInput.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/ClauseTemplates/IClauseTemplateAppService.cs`
- Create: `src/DredgeAI.BidCompare.Application/ClauseTemplates/ClauseTemplateAppService.cs`
- Modify: `src/DredgeAI.BidCompare.Application/BidCompareApplicationAutoMapperProfile.cs`
- Create: `src/DredgeAI.BidCompare.HttpApi/Controllers/ClauseTemplateController.cs`
- Test: `test/DredgeAI.BidCompare.Application.Tests/ClauseTemplates/ClauseTemplateAppServiceTests.cs`

**Steps:**

- [ ] **Step 1: 写失败测试（分页/创建返回完整 DTO/全量更新/删除）**

  创建 `test/DredgeAI.BidCompare.Application.Tests/ClauseTemplates/ClauseTemplateAppServiceTests.cs`：

  ```csharp
  using System;
  using System.Threading.Tasks;
  using Shouldly;
  using Volo.Abp.Domain.Repositories;
  using Xunit;

  namespace DredgeAI.BidCompare.ClauseTemplates;

  public class ClauseTemplateAppServiceTests : BidCompareApplicationTestBase
  {
      private readonly IClauseTemplateAppService _appService;

      public ClauseTemplateAppServiceTests()
      {
          _appService = GetRequiredService<IClauseTemplateAppService>();
      }

      [Fact]
      public async Task Create_Should_Return_Full_Dto()
      {
          var created = await _appService.CreateAsync(new ClauseTemplateCreateUpdateDto
          {
              Text = "须提供 ISO9001 质量管理体系认证证书",
              Mandatory = true,
              Category = "资质"
          });

          created.Id.ShouldNotBe(Guid.Empty);
          created.Text.ShouldContain("ISO9001");
          created.Mandatory.ShouldBeTrue();
          created.Category.ShouldBe("资质");
          created.CreationTime.ShouldBeGreaterThan(DateTime.MinValue);
      }

      [Fact]
      public async Task GetList_Should_Page_And_Filter_By_Keyword()
      {
          await _appService.CreateAsync(new ClauseTemplateCreateUpdateDto { Text = "须提供营业执照", Category = "资质" });
          await _appService.CreateAsync(new ClauseTemplateCreateUpdateDto { Text = "报价不得高于最高限价", Category = "报价" });

          var all = await _appService.GetListAsync(new GetClauseTemplatesInput { MaxResultCount = 10 });
          all.TotalCount.ShouldBe(2);
          all.Items.Count.ShouldBe(2);

          var filtered = await _appService.GetListAsync(new GetClauseTemplatesInput { Keyword = "报价", MaxResultCount = 10 });
          filtered.TotalCount.ShouldBe(1);
          filtered.Items[0].Category.ShouldBe("报价");
      }

      [Fact]
      public async Task Update_Should_Be_Full_Replace_And_Return_Dto()
      {
          var created = await _appService.CreateAsync(new ClauseTemplateCreateUpdateDto { Text = "旧文本" });

          var updated = await _appService.UpdateAsync(created.Id, new ClauseTemplateCreateUpdateDto
          {
              Text = "新文本",
              Mandatory = false,
              Category = "格式"
          });

          updated.Id.ShouldBe(created.Id);
          updated.Text.ShouldBe("新文本");
          updated.Mandatory.ShouldBeFalse();
          updated.Category.ShouldBe("格式");
      }

      [Fact]
      public async Task Delete_Should_Remove_Entity()
      {
          var created = await _appService.CreateAsync(new ClauseTemplateCreateUpdateDto { Text = "待删除" });

          await _appService.DeleteAsync(created.Id);

          var repo = GetRequiredService<IRepository<Clauses.ClauseTemplate, Guid>>();
          (await repo.FindAsync(created.Id)).ShouldBeNull();
      }
  }
  ```

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter ClauseTemplateAppServiceTests
  ```

  预期：**编译失败**。

- [ ] **Step 2: 创建 Contracts**

  `ClauseTemplates/ClauseTemplateDto.cs`（响应含审计字段，遵循 ABP 标准 §4）：

  ```csharp
  using System;
  using Volo.Abp.Application.Dtos;

  namespace DredgeAI.BidCompare.ClauseTemplates;

  public class ClauseTemplateDto : AuditedEntityDto<Guid>
  {
      public string Text { get; set; } = default!;

      public bool Mandatory { get; set; }

      public string? Category { get; set; }
  }
  ```

  `ClauseTemplates/ClauseTemplateCreateUpdateDto.cs`（创建/更新共享，ABP 标准 §7.2）：

  ```csharp
  using System.ComponentModel.DataAnnotations;

  namespace DredgeAI.BidCompare.ClauseTemplates;

  public class ClauseTemplateCreateUpdateDto
  {
      [Required]
      [StringLength(2000)]
      public string Text { get; set; } = default!;

      public bool Mandatory { get; set; } = true;

      [StringLength(64)]
      public string? Category { get; set; }
  }
  ```

  `ClauseTemplates/GetClauseTemplatesInput.cs`：

  ```csharp
  using Volo.Abp.Application.Dtos;

  namespace DredgeAI.BidCompare.ClauseTemplates;

  public class GetClauseTemplatesInput : PagedAndSortedResultRequestDto
  {
      public string? Keyword { get; set; }

      public string? Category { get; set; }
  }
  ```

  `ClauseTemplates/IClauseTemplateAppService.cs`：

  ```csharp
  using System;
  using System.Threading.Tasks;
  using Volo.Abp.Application.Dtos;
  using Volo.Abp.Application.Services;

  namespace DredgeAI.BidCompare.ClauseTemplates;

  public interface IClauseTemplateAppService : IApplicationService
  {
      Task<PagedResultDto<ClauseTemplateDto>> GetListAsync(GetClauseTemplatesInput input);

      Task<ClauseTemplateDto> GetAsync(Guid id);

      Task<ClauseTemplateDto> CreateAsync(ClauseTemplateCreateUpdateDto input);

      Task<ClauseTemplateDto> UpdateAsync(Guid id, ClauseTemplateCreateUpdateDto input);

      Task DeleteAsync(Guid id);
  }
  ```

- [ ] **Step 3: 实现 ClauseTemplateAppService + AutoMapper 映射**

  在 `src/DredgeAI.BidCompare.Application/BidCompareApplicationAutoMapperProfile.cs` 的构造函数中追加：

  ```csharp
  CreateMap<Clauses.ClauseTemplate, ClauseTemplates.ClauseTemplateDto>();
  ```

  创建 `src/DredgeAI.BidCompare.Application/ClauseTemplates/ClauseTemplateAppService.cs`：

  ```csharp
  using System;
  using System.Linq;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.Clauses;
  using Volo.Abp;
  using Volo.Abp.Application.Dtos;
  using Volo.Abp.Application.Services;
  using Volo.Abp.Domain.Repositories;

  namespace DredgeAI.BidCompare.ClauseTemplates;

  [RemoteService(false)]
  public class ClauseTemplateAppService : ApplicationService, IClauseTemplateAppService
  {
      private readonly IRepository<ClauseTemplate, Guid> _repository;

      public ClauseTemplateAppService(IRepository<ClauseTemplate, Guid> repository)
      {
          _repository = repository;
      }

      public async Task<PagedResultDto<ClauseTemplateDto>> GetListAsync(GetClauseTemplatesInput input)
      {
          var queryable = await _repository.GetQueryableAsync();
          queryable = queryable
              .WhereIf(!input.Keyword.IsNullOrWhiteSpace(), x => x.Text.Contains(input.Keyword!))
              .WhereIf(!input.Category.IsNullOrWhiteSpace(), x => x.Category == input.Category);

          var totalCount = await AsyncExecuter.CountAsync(queryable);
          var items = await AsyncExecuter.ToListAsync(queryable
              .OrderByDescending(x => x.CreationTime)
              .PageBy(input.SkipCount, input.MaxResultCount));

          return new PagedResultDto<ClauseTemplateDto>(
              totalCount,
              items.Select(x => ObjectMapper.Map<ClauseTemplate, ClauseTemplateDto>(x)).ToList());
      }

      public async Task<ClauseTemplateDto> GetAsync(Guid id)
      {
          var entity = await _repository.GetAsync(id);
          return ObjectMapper.Map<ClauseTemplate, ClauseTemplateDto>(entity);
      }

      public async Task<ClauseTemplateDto> CreateAsync(ClauseTemplateCreateUpdateDto input)
      {
          var entity = new ClauseTemplate(GuidGenerator.Create(), input.Text.Trim(), input.Mandatory, input.Category);
          await _repository.InsertAsync(entity, autoSave: true);
          return ObjectMapper.Map<ClauseTemplate, ClauseTemplateDto>(entity);
      }

      public async Task<ClauseTemplateDto> UpdateAsync(Guid id, ClauseTemplateCreateUpdateDto input)
      {
          var entity = await _repository.GetAsync(id);
          entity.Update(input.Text.Trim(), input.Mandatory, input.Category);
          await _repository.UpdateAsync(entity, autoSave: true);
          return ObjectMapper.Map<ClauseTemplate, ClauseTemplateDto>(entity);
      }

      public async Task DeleteAsync(Guid id)
      {
          await _repository.DeleteAsync(id, autoSave: true);
      }
  }
  ```

- [ ] **Step 4: 实现 ClauseTemplateController**

  创建 `src/DredgeAI.BidCompare.HttpApi/Controllers/ClauseTemplateController.cs`：

  ```csharp
  using System;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.ClauseTemplates;
  using Microsoft.AspNetCore.Mvc;
  using Volo.Abp.Application.Dtos;
  using Volo.Abp.AspNetCore.Mvc;

  namespace DredgeAI.BidCompare.Controllers;

  [Area("compare")]
  [Route("api/compare/clause-templates")]
  public class ClauseTemplateController : AbpControllerBase
  {
      private readonly IClauseTemplateAppService _appService;

      public ClauseTemplateController(IClauseTemplateAppService appService)
      {
          _appService = appService;
      }

      /// <summary>GET /api/compare/clause-templates 个人条款库（分页）</summary>
      [HttpGet]
      public Task<PagedResultDto<ClauseTemplateDto>> GetListAsync([FromQuery] GetClauseTemplatesInput input)
          => _appService.GetListAsync(input);

      [HttpGet("{id}")]
      public Task<ClauseTemplateDto> GetAsync(Guid id)
          => _appService.GetAsync(id);

      /// <summary>POST /api/compare/clause-templates 新增条款模板</summary>
      [HttpPost]
      public Task<ClauseTemplateDto> CreateAsync([FromBody] ClauseTemplateCreateUpdateDto input)
          => _appService.CreateAsync(input);

      /// <summary>PUT /api/compare/clause-templates/{id}（补充路由，全量更新）</summary>
      [HttpPut("{id}")]
      public Task<ClauseTemplateDto> UpdateAsync(Guid id, [FromBody] ClauseTemplateCreateUpdateDto input)
          => _appService.UpdateAsync(id, input);

      /// <summary>DELETE /api/compare/clause-templates/{id}（补充路由）</summary>
      [HttpDelete("{id}")]
      public async Task<IActionResult> DeleteAsync(Guid id)
      {
          await _appService.DeleteAsync(id);
          return NoContent();
      }
  }
  ```

- [ ] **Step 5: 跑测试确认通过并提交**

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter ClauseTemplateAppServiceTests
  ```

  预期：4 passed。

  ```bash
  git add backend/DredgeAI.BidCompare
  git commit -m "feat(backend): add clause template library CRUD API (/api/compare/clause-templates)"
  ```

---

## Task 11 【P2】ILlmGateway：条款提取（extract）与条款确认锁定（PUT clauses）

覆盖 spec §6 路由：`POST /api/compare/tasks/{id}/clauses/extract`、`PUT /api/compare/tasks/{id}/clauses`。语义见 spec §3.1 compare-ai（强制性条款提取）与 §6.2（条款清单做成任务内快照，确认后锁定）。

**Files:**
- Create: `src/DredgeAI.BidCompare.Domain/AI/ILlmGateway.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/Clauses/ConfirmClausesInput.cs`
- Modify: `src/DredgeAI.BidCompare.Application.Contracts/CompareTasks/ICompareTaskAppService.cs`
- Modify: `src/DredgeAI.BidCompare.Application/CompareTasks/CompareTaskAppService.cs`
- Modify: `src/DredgeAI.BidCompare.HttpApi/Controllers/CompareTaskController.cs`
- Create: `src/DredgeAI.BidCompare.HttpApi.Host/AI/LlmOptions.cs`
- Create: `src/DredgeAI.BidCompare.HttpApi.Host/AI/OpenAiCompatibleLlmGateway.cs`
- Modify: `src/DredgeAI.BidCompare.HttpApi.Host/BidCompareHttpApiHostModule.cs`
- Modify: `src/DredgeAI.BidCompare.HttpApi.Host/appsettings.json`
- Test: `test/DredgeAI.BidCompare.TestBase/Fakes/FakeLlmGateway.cs`
- Modify: `test/DredgeAI.BidCompare.Application.Tests/BidCompareApplicationTestModule.cs`
- Test: `test/DredgeAI.BidCompare.Application.Tests/Clauses/ClauseExtractionTests.cs`

**Steps:**

- [ ] **Step 1: 定义 ILlmGateway + FakeLlmGateway + ConfirmClausesInput**

  创建 `src/DredgeAI.BidCompare.Domain/AI/ILlmGateway.cs`：

  ```csharp
  using System.Threading;
  using System.Threading.Tasks;

  namespace DredgeAI.BidCompare.AI;

  /// <summary>
  /// LLM 网关（OpenAI 兼容协议，可配置 endpoint/model/key）。
  /// 上层（条款提取/响应判定/指标抽取）负责 prompt 与响应 JSON 解析，网关只做对话补全。
  /// </summary>
  public interface ILlmGateway
  {
      /// <summary>单次对话补全，返回 assistant 文本内容。</summary>
      Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
  }
  ```

  创建 `test/DredgeAI.BidCompare.TestBase/Fakes/FakeLlmGateway.cs`：

  ```csharp
  using System.Collections.Generic;
  using System.Threading;
  using System.Threading.Tasks;

  namespace DredgeAI.BidCompare.AI;

  /// <summary>队列式 Fake：按调用顺序返回 QueueResponse 预置的响应；耗尽即抛异常暴露未预期调用。</summary>
  public class FakeLlmGateway : ILlmGateway
  {
      private readonly Queue<string> _responses = new();

      public List<(string System, string User)> Requests { get; } = new();

      public void QueueResponse(string response) => _responses.Enqueue(response);

      public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
      {
          Requests.Add((systemPrompt, userPrompt));
          if (_responses.Count == 0)
          {
              throw new System.InvalidOperationException("FakeLlmGateway：响应队列已空，存在未预期的 LLM 调用");
          }
          return Task.FromResult(_responses.Dequeue());
      }
  }
  ```

  在 `BidCompareApplicationTestModule.ConfigureServices` 标注处追加（`using DredgeAI.BidCompare.AI;` 一并追加）：

  ```csharp
  context.Services.Replace(ServiceDescriptor.Singleton<ILlmGateway, FakeLlmGateway>());
  ```

  创建 `src/DredgeAI.BidCompare.Application.Contracts/Clauses/ConfirmClausesInput.cs`：

  ```csharp
  using System.Collections.Generic;
  using System.ComponentModel.DataAnnotations;

  namespace DredgeAI.BidCompare.Clauses;

  /// <summary>PUT clauses 请求体：用户确认后的条款清单（全量，含勾选/编辑/从条款库追加的结果）。</summary>
  public class ConfirmClausesInput
  {
      [Required]
      [MinLength(1)]
      public List<ClauseInputDto> Clauses { get; set; } = new();
  }
  ```

- [ ] **Step 2: 写失败测试（提取草案不落库 / 确认锁定快照并触发比对 / 无招标文件报错）**

  创建 `test/DredgeAI.BidCompare.Application.Tests/Clauses/ClauseExtractionTests.cs`：

  ```csharp
  using System;
  using System.IO;
  using System.Linq;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.AI;
  using DredgeAI.BidCompare.BackgroundJobs;
  using DredgeAI.BidCompare.Clauses;
  using DredgeAI.BidCompare.CompareTasks;
  using DredgeAI.BidCompare.Documents;
  using Shouldly;
  using Volo.Abp;
  using Volo.Abp.BackgroundJobs;
  using Xunit;

  namespace DredgeAI.BidCompare.Clauses;

  public class ClauseExtractionTests : BidCompareApplicationTestBase
  {
      private readonly ICompareTaskAppService _appService;
      private readonly FakeLlmGateway _llmGateway;
      private readonly RecordingBackgroundJobManager _jobManager;

      public ClauseExtractionTests()
      {
          _appService = GetRequiredService<ICompareTaskAppService>();
          _llmGateway = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
          _jobManager = (RecordingBackgroundJobManager)GetRequiredService<IBackgroundJobManager>();
      }

      private async Task<(Guid TaskId, Guid TenderId, Guid BidId)> PrepareAwaitingClausesTaskAsync()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
          var tender = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Tender, "招标文件.pdf", new MemoryStream(new byte[] { 1 }));
          var bid = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", new MemoryStream(new byte[] { 2 }));
          var parseJob = GetRequiredService<ParseDocumentJob>();
          await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = tender.Id });
          await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = bid.Id });
          return (task.Id, tender.Id, bid.Id);
      }

      [Fact]
      public async Task Extract_Should_Return_Draft_Without_Persisting()
      {
          var (taskId, _, _) = await PrepareAwaitingClausesTaskAsync();
          _llmGateway.QueueResponse("""
          ```json
          [
            { "text": "投标人须具备建筑工程施工总承包一级资质", "mandatory": true, "category": "资质" },
            { "text": "工期不得超过 180 日历天", "mandatory": true, "category": "工期" }
          ]
          ```
          """);

          var drafts = await _appService.ExtractClausesAsync(taskId);

          drafts.Count.ShouldBe(2);
          drafts.ShouldAllBe(d => d.Source == ClauseSource.Extracted);
          drafts.ShouldAllBe(d => !string.IsNullOrWhiteSpace(d.ClauseId));
          drafts[0].Text.ShouldContain("总承包一级资质");
          drafts[0].Mandatory.ShouldBeTrue();
          drafts[1].Category.ShouldBe("工期");

          // 草案不落库：任务仍处于待确认，快照仍为空（spec §3.2：AI 提取不当黑盒）
          var detail = await _appService.GetAsync(taskId);
          detail.Status.ShouldBe(CompareTaskStatus.AwaitingClauses);
          detail.ClauseSnapshot.ShouldBeNull();

          // prompt 中应带招标文件 content.md 内容
          _llmGateway.Requests.Single().User.ShouldContain("第三章 技术方案");
      }

      [Fact]
      public async Task Extract_Without_TenderDoc_Should_Throw()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });

          var ex = await Should.ThrowAsync<BusinessException>(() => _appService.ExtractClausesAsync(task.Id));
          ex.Code.ShouldBe(BidCompareErrorCodes.NoTenderDocument);
      }

      [Fact]
      public async Task ConfirmClauses_Should_Lock_Snapshot_And_Start_Comparing()
      {
          var (taskId, _, _) = await PrepareAwaitingClausesTaskAsync();
          _jobManager.Clear();

          var result = await _appService.ConfirmClausesAsync(taskId, new ConfirmClausesInput
          {
              Clauses = new()
              {
                  new ClauseInputDto { Text = "AI 草案条款", Source = ClauseSource.Extracted, Mandatory = true, Category = "资质" },
                  new ClauseInputDto { Text = "手动补充条款", Mandatory = true },
                  new ClauseInputDto { ClauseId = "tpl-001", Text = "条款库条款", Source = ClauseSource.Template, Mandatory = false }
              }
          });

          result.Status.ShouldBe(CompareTaskStatus.Comparing); // spec §5 步骤3→4
          result.ClauseSnapshot.ShouldNotBeNull();
          result.ClauseSnapshot!.Count.ShouldBe(3);
          result.ClauseSnapshot[0].Source.ShouldBe(ClauseSource.Extracted);
          result.ClauseSnapshot[1].Source.ShouldBe(ClauseSource.Manual);
          result.ClauseSnapshot[2].ClauseId.ShouldBe("tpl-001"); // 模板条款保留原 id
          result.ClauseSnapshot[2].Mandatory.ShouldBeFalse();

          _jobManager.LastEnqueued<CompareDocumentsArgs>().ShouldNotBeNull();

          // 快照已锁定：再次确认应被状态机拒绝
          await Should.ThrowAsync<BusinessException>(() =>
              _appService.ConfirmClausesAsync(taskId, new ConfirmClausesInput
              {
                  Clauses = new() { new ClauseInputDto { Text = "x" } }
              }));
      }
  }
  ```

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter ClauseExtractionTests
  ```

  预期：**编译失败**（`ExtractClausesAsync`/`ConfirmClausesAsync` 不存在）。

- [ ] **Step 3: AppService 追加 ExtractClausesAsync / ConfirmClausesAsync**

  `ICompareTaskAppService` 追加（`using System.Collections.Generic;`、`using DredgeAI.BidCompare.Clauses;`）：

  ```csharp
  Task<List<ClauseDto>> ExtractClausesAsync(Guid id);

  Task<CompareTaskDto> ConfirmClausesAsync(Guid id, ConfirmClausesInput input);
  ```

  `CompareTaskAppService`：

  - 构造函数追加注入 `ILlmGateway llmGateway` 参数并赋值 `_llmGateway` 字段（`using DredgeAI.BidCompare.AI;`），同步修改 Task 7 的构造函数签名（全部注入处只有 DI 容器调用，无手工 new，安全）。
  - 类内追加：

  ```csharp
  private const string ClauseExtractionSystemPrompt =
      "你是招投标文件分析助手。从用户提供的招标文件全文中提取所有强制性条款" +
      "（包含「须/应当/必须/不得/否则视为无效投标/废标」等强制措辞的条款）。" +
      "只返回 JSON 数组，不要输出任何其他文字。";

  public async Task<List<ClauseDto>> ExtractClausesAsync(Guid id)
  {
      var task = await _taskRepository.GetAsync(id);
      if (!task.TenderDocumentId.HasValue)
      {
          throw new BusinessException(BidCompareErrorCodes.NoTenderDocument).WithData("taskId", id);
      }

      var tenderDoc = await _documentRepository.GetAsync(task.TenderDocumentId.Value);
      if (tenderDoc.ParseStatus != DocumentParseStatus.Parsed || tenderDoc.DocMdStorageKey == null)
      {
          throw new BusinessException(BidCompareErrorCodes.IrNotReady).WithData("docId", tenderDoc.Id);
      }

      string docMd;
      await using (var stream = await _fileStorage.GetAsync(tenderDoc.DocMdStorageKey))
      using (var reader = new StreamReader(stream))
      {
          docMd = await reader.ReadToEndAsync();
      }

      var userPrompt =
          "以下是招标文件全文（Markdown）：\n\n" + docMd +
          "\n\n请以 JSON 数组返回全部强制性条款，每项字段：text（条款原文）、mandatory（是否强制，bool）、category（分类，如 资质/报价/技术/工期/格式）。只返回 JSON。";

      var response = await _llmGateway.CompleteAsync(ClauseExtractionSystemPrompt, userPrompt);

      return ParseClauseDrafts(response);
  }

  public async Task<CompareTaskDto> ConfirmClausesAsync(Guid id, ConfirmClausesInput input)
  {
      var task = await _taskRepository.GetAsync(id);
      var snapshot = BuildSnapshot(input.Clauses);
      task.LockClauseSnapshot(JsonSerializer.Serialize(snapshot, SnapshotJsonOptions));
      task.MarkComparing();
      task.UpdateProgress("comparing", 60, "两两比对中");
      await _taskRepository.UpdateAsync(task, autoSave: true);

      await _backgroundJobManager.EnqueueAsync(new CompareDocumentsArgs { TaskId = id });

      var documents = await GetTaskDocumentsAsync(id);
      return MapToDto(task, documents);
  }

  /// <summary>解析 LLM 条款提取响应：剥离 ```json 围栏后按数组解析，异常即抛 IrValidationFailed。</summary>
  internal static List<ClauseDto> ParseClauseDrafts(string llmResponse)
  {
      var json = llmResponse.Trim();
      if (json.StartsWith("```"))
      {
          var firstNewline = json.IndexOf('\n');
          var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
          if (firstNewline > 0 && lastFence > firstNewline)
          {
              json = json[(firstNewline + 1)..lastFence].Trim();
          }
      }

      try
      {
          using var document = JsonDocument.Parse(json);
          var drafts = new List<ClauseDto>();
          foreach (var element in document.RootElement.EnumerateArray())
          {
              var text = element.TryGetProperty("text", out var t) ? t.GetString() : null;
              if (text.IsNullOrWhiteSpace())
              {
                  continue;
              }
              drafts.Add(new ClauseDto
              {
                  ClauseId = Guid.NewGuid().ToString("N"),
                  Source = ClauseSource.Extracted,
                  Text = text!,
                  Mandatory = element.TryGetProperty("mandatory", out var m) && m.ValueKind == JsonValueKind.True,
                  Category = element.TryGetProperty("category", out var c) && c.ValueKind == JsonValueKind.String
                      ? c.GetString()
                      : null
              });
          }
          return drafts;
      }
      catch (JsonException ex)
      {
          throw new BusinessException(BidCompareErrorCodes.IrValidationFailed)
              .WithData("reason", $"LLM 条款提取响应不是合法 JSON：{ex.Message}");
      }
  }
  ```

  （`ConfirmClausesAsync` 中 `LockClauseSnapshot` 只允许 Parsing/Parsed/Partial/AwaitingClauses，`MarkComparing` 允许 Parsed/Partial/AwaitingClauses——重复确认时 `MarkComparing` 从 Comparing 状态抛出 `InvalidTaskState`，与测试断言一致。）

  `CompareTaskController` 追加 action：

  ```csharp
  /// <summary>POST /api/compare/tasks/{id}/clauses/extract 触发从招标文件提取条款草案</summary>
  [HttpPost("{id}/clauses/extract")]
  public Task<List<Clauses.ClauseDto>> ExtractClausesAsync(Guid id)
      => _appService.ExtractClausesAsync(id);

  /// <summary>PUT /api/compare/tasks/{id}/clauses 确认后的条款清单（锁定快照）</summary>
  [HttpPut("{id}/clauses")]
  public Task<CompareTaskDto> ConfirmClausesAsync(Guid id, [FromBody] Clauses.ConfirmClausesInput input)
      => _appService.ConfirmClausesAsync(id, input);
  ```

  （Controller 顶部追加 `using System.Collections.Generic;`。）

- [ ] **Step 4: 实现 OpenAiCompatibleLlmGateway（生产）**

  创建 `src/DredgeAI.BidCompare.HttpApi.Host/AI/LlmOptions.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.AI;

  public class LlmOptions
  {
      /// <summary>OpenAI 兼容端点（不含 /chat/completions），如 https://api.openai.com/v1。</summary>
      public string Endpoint { get; set; } = "https://api.openai.com/v1";

      public string ApiKey { get; set; } = "";

      public string Model { get; set; } = "gpt-4o-mini";

      public int TimeoutSeconds { get; set; } = 120;
  }
  ```

  创建 `src/DredgeAI.BidCompare.HttpApi.Host/AI/OpenAiCompatibleLlmGateway.cs`：

  ```csharp
  using System.Net.Http;
  using System.Net.Http.Headers;
  using System.Net.Http.Json;
  using System.Text.Json;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.Extensions.Options;
  using Volo.Abp;
  using Volo.Abp.DependencyInjection;

  namespace DredgeAI.BidCompare.AI;

  /// <summary>OpenAI 兼容协议实现：POST {Endpoint}/chat/completions。</summary>
  public class OpenAiCompatibleLlmGateway : ILlmGateway, ITransientDependency
  {
      private static readonly JsonSerializerOptions JsonOptions = new()
      {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          PropertyNameCaseInsensitive = true
      };

      private readonly IHttpClientFactory _httpClientFactory;
      private readonly LlmOptions _options;

      public OpenAiCompatibleLlmGateway(IHttpClientFactory httpClientFactory, IOptions<LlmOptions> options)
      {
          _httpClientFactory = httpClientFactory;
          _options = options.Value;
      }

      public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
      {
          var client = _httpClientFactory.CreateClient(nameof(OpenAiCompatibleLlmGateway));
          client.Timeout = System.TimeSpan.FromSeconds(_options.TimeoutSeconds);
          client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

          var request = new
          {
              model = _options.Model,
              temperature = 0.2,
              messages = new object[]
              {
                  new { role = "system", content = systemPrompt },
                  new { role = "user", content = userPrompt }
              }
          };

          using var response = await client.PostAsJsonAsync(
              $"{_options.Endpoint.TrimEnd('/')}/chat/completions", request, JsonOptions, cancellationToken);
          response.EnsureSuccessStatusCode();

          using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
          var content = payload.RootElement
              .GetProperty("choices")[0]
              .GetProperty("message")
              .GetProperty("content")
              .GetString();

          return content
              ?? throw new BusinessException(BidCompareErrorCodes.IrValidationFailed)
                  .WithData("reason", "LLM 响应缺少 choices[0].message.content");
      }
  }
  ```

  在 `BidCompareHttpApiHostModule.ConfigureServices` 末尾追加：

  ```csharp
  Configure<LlmOptions>(context.Configuration.GetSection("Llm"));
  ```

  `appsettings.json` 顶层追加：

  ```json
  "Llm": {
    "Endpoint": "https://api.openai.com/v1",
    "ApiKey": "",
    "Model": "gpt-4o-mini",
    "TimeoutSeconds": 120
  }
  ```

- [ ] **Step 5: 跑测试确认通过并提交**

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter ClauseExtractionTests
  ```

  预期：3 passed。

  ```bash
  git add backend/DredgeAI.BidCompare
  git commit -m "feat(backend): add LLM gateway with clause extraction draft and clause snapshot confirmation"
  ```

---

## Task 12 【P2】AI 分析后台任务：条款响应判定 + 关键指标抽取

实现 spec §3.1 compare-ai 的「逐份标书响应判定、关键指标抽取」与 §9「AI 服务失败不阻塞整体」。状态机尾巴：`comparing → analyzing → done`。

**Files:**
- Create: `src/DredgeAI.BidCompare.Application/BackgroundJobs/AiAnalysisArgs.cs`
- Create: `src/DredgeAI.BidCompare.Application/BackgroundJobs/AiAnalysisJob.cs`
- Modify: `src/DredgeAI.BidCompare.Application/BackgroundJobs/CompareDocumentsJob.cs`（尾部改为入队 AI 分析）
- Test: `test/DredgeAI.BidCompare.Application.Tests/BackgroundJobs/AiAnalysisJobTests.cs`

**Steps:**

- [ ] **Step 1: 写失败测试（条款判定证据 / 指标证据 / aiGenerated 标记 / LLM 失败仍 Done）**

  创建 `test/DredgeAI.BidCompare.Application.Tests/BackgroundJobs/AiAnalysisJobTests.cs`：

  ```csharp
  using System;
  using System.IO;
  using System.Linq;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.AI;
  using DredgeAI.BidCompare.Clauses;
  using DredgeAI.BidCompare.CompareTasks;
  using DredgeAI.BidCompare.Documents;
  using DredgeAI.BidCompare.Evidences;
  using Shouldly;
  using Xunit;

  namespace DredgeAI.BidCompare.BackgroundJobs;

  public class AiAnalysisJobTests : BidCompareApplicationTestBase
  {
      private readonly ICompareTaskAppService _appService;
      private readonly FakeLlmGateway _llmGateway;

      public AiAnalysisJobTests()
      {
          _appService = GetRequiredService<ICompareTaskAppService>();
          _llmGateway = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
      }

      /// <summary>建 2 份标书 + 条款快照 → 解析 → 确认条款 → 比对，任务进入 Analyzing 并排好 AI 证据的 LLM 响应。</summary>
      private async Task<(Guid TaskId, Guid DocA, Guid DocB)> PrepareAnalyzingTaskAsync()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
          var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", new MemoryStream(new byte[] { 1 }));
          var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf", new MemoryStream(new byte[] { 2 }));
          var parseJob = GetRequiredService<ParseDocumentJob>();
          await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docA.Id });
          await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docB.Id });

          await _appService.ConfirmClausesAsync(task.Id, new ConfirmClausesInput
          {
              Clauses = new()
              {
                  new ClauseInputDto { ClauseId = "c1", Text = "须提供 ISO9001 证书", Mandatory = true, Category = "资质" }
              }
          });

          // 条款判定：docA 未响应（High），docB 部分响应（Mid）；随后指标抽取一次
          _llmGateway.QueueResponse("""[{"clauseId":"c1","status":"none","reason":"全文未提及质量管理体系认证","blockIds":["b0001"]}]""");
          _llmGateway.QueueResponse("""[{"clauseId":"c1","status":"partial","reason":"仅承诺投标后补办","blockIds":["b0003"]}]""");
          _llmGateway.QueueResponse("""[{"indicator":"报价","summaries":[{"docId":"DOC_A","summary":"总价 120 万元"},{"docId":"DOC_B","summary":"总价 118 万元"}]}]"""
              .Replace("DOC_A", docA.Id.ToString()).Replace("DOC_B", docB.Id.ToString()));

          await GetRequiredService<CompareDocumentsJob>().ExecuteAsync(new CompareDocumentsArgs { TaskId = task.Id });
          return (task.Id, docA.Id, docB.Id);
      }

      [Fact]
      public async Task AiAnalysis_Should_Persist_Clause_And_Indicator_Evidences_Then_Done()
      {
          var (taskId, docA, docB) = await PrepareAnalyzingTaskAsync();

          await GetRequiredService<AiAnalysisJob>().ExecuteAsync(new AiAnalysisArgs { TaskId = taskId });

          var detail = await _appService.GetAsync(taskId);
          detail.Status.ShouldBe(CompareTaskStatus.Done);
          detail.Progress.Percent.ShouldBe(100);

          var clauseEvidences = await _appService.GetEvidencesAsync(taskId,
              new GetEvidenceListInput { Type = EvidenceType.Clause, MaxResultCount = 10 });
          clauseEvidences.TotalCount.ShouldBe(2);
          clauseEvidences.Items.ShouldAllBe(e => e.AiGenerated); // spec §3.2：AI 结论可区分

          var high = clauseEvidences.Items.Single(e => e.Severity == EvidenceSeverity.High);
          high.DocIds.ShouldBe(new[] { docA });
          high.Locations.Single().DocId.ShouldBe(docA);
          high.Locations.Single().BlockIds.ShouldContain("b0001");
          high.Description.ShouldContain("质量管理体系认证");

          var mid = clauseEvidences.Items.Single(e => e.Severity == EvidenceSeverity.Mid);
          mid.DocIds.ShouldBe(new[] { docB });

          var indicatorEvidences = await _appService.GetEvidencesAsync(taskId,
              new GetEvidenceListInput { Type = EvidenceType.Indicator, MaxResultCount = 10 });
          indicatorEvidences.TotalCount.ShouldBe(1);
          indicatorEvidences.Items[0].Title.ShouldContain("报价");
          indicatorEvidences.Items[0].Description.ShouldContain("120 万元");
          indicatorEvidences.Items[0].AiGenerated.ShouldBeTrue();
      }

      [Fact]
      public async Task Llm_Failure_Should_Not_Block_Task() // spec §9：AI 失败不阻塞整体
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
          var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", new MemoryStream(new byte[] { 1 }));
          var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf", new MemoryStream(new byte[] { 2 }));
          var parseJob = GetRequiredService<ParseDocumentJob>();
          await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docA.Id });
          await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docB.Id });
          await _appService.ConfirmClausesAsync(task.Id, new ConfirmClausesInput
          {
              Clauses = new() { new ClauseInputDto { Text = "x", Mandatory = true } }
          });
          await GetRequiredService<CompareDocumentsJob>().ExecuteAsync(new CompareDocumentsArgs { TaskId = task.Id });
          // 不 QueueResponse → FakeLlmGateway 抛 InvalidOperationException，模拟 AI 服务失败

          await GetRequiredService<AiAnalysisJob>().ExecuteAsync(new AiAnalysisArgs { TaskId = task.Id });

          var detail = await _appService.GetAsync(task.Id);
          detail.Status.ShouldBe(CompareTaskStatus.Done); // 算法证据照常展示
          detail.Progress.Message.ShouldContain("AI 分析暂不可用");
      }
  }
  ```

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter AiAnalysisJobTests
  ```

  预期：**编译失败**（`AiAnalysisJob` 不存在；且 `CompareDocumentsJob` 当前直接 Done，`PrepareAnalyzingTaskAsync` 中断言的 Analyzing 流程不成立）。

- [ ] **Step 2: 修改 CompareDocumentsJob 尾部（MarkAnalyzing 后入队 AiAnalysisJob，不再直接 Done）**

  将 `CompareDocumentsJob.ExecuteAsync` 末尾的：

  ```csharp
  task.MarkAnalyzing();
  task.MarkDone();
  task.UpdateProgress("done", 100, null);
  await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
  ```

  替换为：

  ```csharp
  task.MarkAnalyzing();
  task.UpdateProgress("analyzing", 80, "AI 分析中");
  await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
  await _backgroundJobManager.EnqueueAsync(new AiAnalysisArgs { TaskId = args.TaskId });
  ```

  构造函数追加注入 `IBackgroundJobManager backgroundJobManager` 并赋值 `_backgroundJobManager` 字段（`using Volo.Abp.BackgroundJobs;`）。

  > 注意：此改动使 Task 9 测试 `Compare_Job_Should_Persist_Evidences_And_Finish_Task` 的断言 `Status == Done` 变为 `Analyzing`、`Progress.Percent == 100` 变为 `80`——同步修改该测试两处断言为 `CompareTaskStatus.Analyzing` 与 `80`（Done 的最终断言由 Task 12 的 AiAnalysisJobTests 覆盖）。

- [ ] **Step 3: 实现 AiAnalysisJob**

  `src/DredgeAI.BidCompare.Application/BackgroundJobs/AiAnalysisArgs.cs`：

  ```csharp
  using System;

  namespace DredgeAI.BidCompare.BackgroundJobs;

  public class AiAnalysisArgs
  {
      public Guid TaskId { get; set; }
  }
  ```

  `src/DredgeAI.BidCompare.Application/BackgroundJobs/AiAnalysisJob.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text.Json;
  using System.Threading;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.AI;
  using DredgeAI.BidCompare.Clauses;
  using DredgeAI.BidCompare.CompareTasks;
  using DredgeAI.BidCompare.Documents;
  using DredgeAI.BidCompare.Evidences;
  using DredgeAI.BidCompare.Storage;
  using Microsoft.Extensions.Logging;
  using Volo.Abp;
  using Volo.Abp.BackgroundJobs;
  using Volo.Abp.DependencyInjection;
  using Volo.Abp.Domain.Repositories;
  using Volo.Abp.Guids;
  using Volo.Abp.Linq;

  namespace DredgeAI.BidCompare.BackgroundJobs;

  /// <summary>
  /// AI 分析（spec §5 步骤4 后半段）：逐份标书条款响应判定 + 关键指标抽取。
  /// spec §9：AI 失败/超时 → 算法证据照常展示，任务仍 Done，进度信息标注「AI 分析暂不可用」。
  /// </summary>
  public class AiAnalysisJob : AsyncBackgroundJob<AiAnalysisArgs>, ITransientDependency
  {
      private const int DocMdMaxChars = 20000;      // 条款判定单份截断
      private const int IndicatorDocMaxChars = 8000; // 指标抽取单份截断

      private const string ClauseJudgementSystemPrompt =
          "你是招投标评审助手。给定一条强制性条款与一份标书全文，判断该标书是否实质响应此条款。" +
          "只返回 JSON 数组，不要输出任何其他文字。";

      private const string IndicatorSystemPrompt =
          "你是招投标评审助手。从多份标书中抽取关键指标（报价、工期、资质、技术方案要点等）用于比选。" +
          "只返回 JSON 数组，不要输出任何其他文字。";

      private readonly IRepository<CompareTask, Guid> _taskRepository;
      private readonly IRepository<CompareDocument, Guid> _documentRepository;
      private readonly IRepository<EvidenceItem, Guid> _evidenceRepository;
      private readonly IFileStorage _fileStorage;
      private readonly ILlmGateway _llmGateway;
      private readonly IAsyncQueryableExecuter _asyncExecuter;
      private readonly IGuidGenerator _guidGenerator;

      public AiAnalysisJob(
          IRepository<CompareTask, Guid> taskRepository,
          IRepository<CompareDocument, Guid> documentRepository,
          IRepository<EvidenceItem, Guid> evidenceRepository,
          IFileStorage fileStorage,
          ILlmGateway llmGateway,
          IAsyncQueryableExecuter asyncExecuter,
          IGuidGenerator guidGenerator)
      {
          _taskRepository = taskRepository;
          _documentRepository = documentRepository;
          _evidenceRepository = evidenceRepository;
          _fileStorage = fileStorage;
          _llmGateway = llmGateway;
          _asyncExecuter = asyncExecuter;
          _guidGenerator = guidGenerator;
      }

      public override async Task ExecuteAsync(AiAnalysisArgs args, CancellationToken cancellationToken = default)
      {
          var task = await _taskRepository.GetAsync(args.TaskId, cancellationToken: cancellationToken);

          try
          {
              var queryable = await _documentRepository.GetQueryableAsync();
              var bidDocs = await _asyncExecuter.ToListAsync(queryable.Where(d =>
                  d.TaskId == args.TaskId &&
                  d.Role == DocumentRole.Bid &&
                  d.ParseStatus == DocumentParseStatus.Parsed));

              var docMds = new Dictionary<CompareDocument, string>();
              foreach (var doc in bidDocs.Where(d => d.DocMdStorageKey != null))
              {
                  await using var stream = await _fileStorage.GetAsync(doc.DocMdStorageKey!, cancellationToken);
                  using var reader = new StreamReader(stream);
                  docMds[doc] = await reader.ReadToEndAsync(cancellationToken);
              }

              var snapshot = task.ClauseSnapshotJson == null
                  ? new List<ClauseSnapshotItem>()
                  : JsonSerializer.Deserialize<List<ClauseSnapshotItem>>(
                      task.ClauseSnapshotJson, CompareTaskAppService.SnapshotJsonOptions) ?? new();

              if (snapshot.Count > 0)
              {
                  await RunClauseJudgementAsync(args.TaskId, snapshot, docMds, cancellationToken);
              }

              if (docMds.Count > 0)
              {
                  await RunIndicatorExtractionAsync(args.TaskId, docMds, cancellationToken);
              }

              task.UpdateProgress("done", 100, null);
          }
          catch (Exception ex) when (ex is not OperationCanceledException)
          {
              // spec §9：AI 区块显示「AI 分析暂不可用」，不阻塞整体
              Logger.LogWarning(ex, "任务 {TaskId} AI 分析失败，降级为仅算法证据", args.TaskId);
              task.UpdateProgress("done", 100, "AI 分析暂不可用，可重新触发条款确认以重试");
          }

          task.MarkDone();
          await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
      }

      private async Task RunClauseJudgementAsync(
          Guid taskId,
          List<ClauseSnapshotItem> snapshot,
          Dictionary<CompareDocument, string> docMds,
          CancellationToken cancellationToken)
      {
          var clausesJson = JsonSerializer.Serialize(
              snapshot.Select(c => new { c.ClauseId, c.Text, c.Mandatory }),
              CompareTaskAppService.SnapshotJsonOptions);

          foreach (var (doc, docMd) in docMds)
          {
              var userPrompt =
                  "强制性条款清单（JSON）：\n" + clausesJson +
                  "\n\n标书全文（Markdown，可能截断）：\n" + Truncate(docMd, DocMdMaxChars) +
                  "\n\n请逐条判定，以 JSON 数组返回，每项字段：clauseId、status（responded=实质响应 / partial=部分响应 / none=未响应）、reason（判定理由）、blockIds（相关原文块 id 数组，可为空）。只返回 JSON。";

              var response = await _llmGateway.CompleteAsync(ClauseJudgementSystemPrompt, userPrompt, cancellationToken);

              foreach (var judgement in ParseJudgements(response))
              {
                  if (judgement.Status == "responded")
                  {
                      continue; // 响应正常不产证据
                  }
                  var clause = snapshot.FirstOrDefault(c => c.ClauseId == judgement.ClauseId);
                  var mandatory = clause?.Mandatory ?? true;
                  var severity = (mandatory, judgement.Status) switch
                  {
                      (true, "none") => EvidenceSeverity.High,
                      (true, "partial") => EvidenceSeverity.Mid,
                      (false, "none") => EvidenceSeverity.Mid,
                      _ => EvidenceSeverity.Low
                  };

                  await _evidenceRepository.InsertAsync(new EvidenceItem(
                      _guidGenerator.Create(),
                      taskId,
                      EvidenceType.Clause,
                      severity,
                      EvidenceMapper.SerializeDocIds(new[] { doc.Id }),
                      EvidenceMapper.SerializeLocations(new[]
                      {
                          new EvidenceLocationDto { DocId = doc.Id, BlockIds = judgement.BlockIds }
                      }),
                      metricsJson: null,
                      title: $"条款未实质响应（{doc.FileName}）：{clause?.Text ?? judgement.ClauseId}",
                      description: judgement.Reason,
                      aiGenerated: true), cancellationToken: cancellationToken);
              }
          }
      }

      private async Task RunIndicatorExtractionAsync(
          Guid taskId,
          Dictionary<CompareDocument, string> docMds,
          CancellationToken cancellationToken)
      {
          var docsSection = string.Join("\n\n", docMds.Select(kv =>
              $"=== 标书 docId={kv.Key.Id}（{kv.Key.FileName}）===\n{Truncate(kv.Value, IndicatorDocMaxChars)}"));

          var userPrompt =
              docsSection +
              "\n\n请抽取关键指标，以 JSON 数组返回，每项字段：indicator（指标名）、summaries（数组，每项含 docId、summary）。只返回 JSON。";

          var response = await _llmGateway.CompleteAsync(IndicatorSystemPrompt, userPrompt, cancellationToken);

          foreach (var indicator in ParseIndicators(response))
          {
              var relatedDocIds = indicator.Summaries
                  .Select(s => Guid.TryParse(s.DocId, out var id) ? id : Guid.Empty)
                  .Where(id => id != Guid.Empty)
                  .ToList();

              await _evidenceRepository.InsertAsync(new EvidenceItem(
                  _guidGenerator.Create(),
                  taskId,
                  EvidenceType.Indicator,
                  EvidenceSeverity.Low,
                  EvidenceMapper.SerializeDocIds(relatedDocIds),
                  EvidenceMapper.SerializeLocations(Enumerable.Empty<EvidenceLocationDto>()),
                  metricsJson: null,
                  title: $"指标比选：{indicator.Indicator}",
                  description: string.Join("；", indicator.Summaries.Select(s => $"{s.DocId}: {s.Summary}")),
                  aiGenerated: true), cancellationToken: cancellationToken);
          }
      }

      private static string Truncate(string text, int maxChars)
          => text.Length <= maxChars ? text : text[..maxChars] + "\n（截断）";

      private static List<ClauseJudgement> ParseJudgements(string llmResponse)
      {
          try
          {
              using var document = JsonDocument.Parse(StripFence(llmResponse));
              var result = new List<ClauseJudgement>();
              foreach (var element in document.RootElement.EnumerateArray())
              {
                  result.Add(new ClauseJudgement(
                      element.TryGetProperty("clauseId", out var c) ? c.GetString() ?? "" : "",
                      element.TryGetProperty("status", out var s) ? s.GetString()?.ToLowerInvariant() ?? "none" : "none",
                      element.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "",
                      element.TryGetProperty("blockIds", out var b) && b.ValueKind == JsonValueKind.Array
                          ? b.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x != "").ToList()
                          : new List<string>()));
              }
              return result;
          }
          catch (JsonException ex)
          {
              throw new BusinessException(BidCompareErrorCodes.IrValidationFailed)
                  .WithData("reason", $"LLM 条款判定响应不是合法 JSON：{ex.Message}");
          }
      }

      private static List<IndicatorItem> ParseIndicators(string llmResponse)
      {
          try
          {
              using var document = JsonDocument.Parse(StripFence(llmResponse));
              var result = new List<IndicatorItem>();
              foreach (var element in document.RootElement.EnumerateArray())
              {
                  var summaries = new List<IndicatorSummary>();
                  if (element.TryGetProperty("summaries", out var arr) && arr.ValueKind == JsonValueKind.Array)
                  {
                      foreach (var s in arr.EnumerateArray())
                      {
                          summaries.Add(new IndicatorSummary(
                              s.TryGetProperty("docId", out var d) ? d.GetString() ?? "" : "",
                              s.TryGetProperty("summary", out var m) ? m.GetString() ?? "" : ""));
                      }
                  }
                  result.Add(new IndicatorItem(
                      element.TryGetProperty("indicator", out var i) ? i.GetString() ?? "未命名指标" : "未命名指标",
                      summaries));
              }
              return result;
          }
          catch (JsonException ex)
          {
              throw new BusinessException(BidCompareErrorCodes.IrValidationFailed)
                  .WithData("reason", $"LLM 指标抽取响应不是合法 JSON：{ex.Message}");
          }
      }

      private static string StripFence(string text)
      {
          var json = text.Trim();
          if (json.StartsWith("```"))
          {
              var firstNewline = json.IndexOf('\n');
              var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
              if (firstNewline > 0 && lastFence > firstNewline)
              {
                  json = json[(firstNewline + 1)..lastFence].Trim();
              }
          }
          return json;
      }

      private record ClauseJudgement(string ClauseId, string Status, string Reason, List<string> BlockIds);

      private record IndicatorItem(string Indicator, List<IndicatorSummary> Summaries);

      private record IndicatorSummary(string DocId, string Summary);
  }
  ```

- [ ] **Step 4: 跑测试确认通过**

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter "AiAnalysisJobTests|CompareDocumentsJobTests"
  ```

  预期：AiAnalysisJobTests 2 passed + CompareDocumentsJobTests 5 passed（含已修正的 Analyzing 断言）。

- [ ] **Step 5: 提交**

  ```bash
  git add backend/DredgeAI.BidCompare
  git commit -m "feat(backend): add AI analysis job for clause judgement and indicator extraction (analyzing -> done)"
  ```

---

## Task 13 【P2】报告 JSON 组装与查询 API

覆盖 spec §6 路由：`GET /api/compare/tasks/{id}/report`。报告结构遵循 spec §8（摘要/相似度矩阵/围标风险/条款响应/指标比选），一致性原则：每条报告证据与结果工作台同一证据 ID 对应。

**Files:**
- Create: `src/DredgeAI.BidCompare.Application.Contracts/Reports/CompareReportDto.cs`
- Create: `src/DredgeAI.BidCompare.Application/Reporting/ReportBuilder.cs`
- Modify: `src/DredgeAI.BidCompare.Application.Contracts/CompareTasks/ICompareTaskAppService.cs`
- Modify: `src/DredgeAI.BidCompare.Application/CompareTasks/CompareTaskAppService.cs`
- Modify: `src/DredgeAI.BidCompare.HttpApi/Controllers/CompareTaskController.cs`
- Test: `test/DredgeAI.BidCompare.Application.Tests/Reports/ReportTests.cs`

**Steps:**

- [ ] **Step 1: 创建报告 DTO（spec §6.1 CompareReport 逐字段）**

  创建 `src/DredgeAI.BidCompare.Application.Contracts/Reports/CompareReportDto.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;
  using DredgeAI.BidCompare.Analysis;
  using DredgeAI.BidCompare.Evidences;

  namespace DredgeAI.BidCompare.Reports;

  /// <summary>spec §6.1：CompareReport { taskId, summary, matrix, sections, generatedAt }。</summary>
  public class CompareReportDto
  {
      public Guid TaskId { get; set; }

      public ReportSummaryDto Summary { get; set; } = new();

      public SimilarityMatrixDto Matrix { get; set; } = new();

      /// <summary>固定三节：bidRiggingRisk（围标风险）/ clauseCompliance（条款响应）/ indicatorComparison（指标比选）。</summary>
      public List<ReportSectionDto> Sections { get; set; } = new();

      public DateTime GeneratedAt { get; set; }
  }

  public class ReportSummaryDto
  {
      public int DocCount { get; set; }

      public int HighRiskCount { get; set; }

      public int MidRiskCount { get; set; }

      public int LowRiskCount { get; set; }

      /// <summary>spec §8-2：Top 5 最重要发现（按严重度+时间排序的标题）。</summary>
      public List<string> TopFindings { get; set; } = new();
  }

  public class ReportSectionDto
  {
      public string Key { get; set; } = default!;

      public string Title { get; set; } = default!;

      /// <summary>证据与结果工作台同源（spec §8 一致性原则：同一证据 ID）。</summary>
      public List<EvidenceDto> Evidences { get; set; } = new();
  }
  ```

- [ ] **Step 2: 写失败测试（Done 后组装+缓存 / 未 Done 报 ReportNotReady / 三节归类）**

  创建 `test/DredgeAI.BidCompare.Application.Tests/Reports/ReportTests.cs`：

  ```csharp
  using System;
  using System.IO;
  using System.Linq;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.AI;
  using DredgeAI.BidCompare.BackgroundJobs;
  using DredgeAI.BidCompare.Clauses;
  using DredgeAI.BidCompare.CompareTasks;
  using DredgeAI.BidCompare.Documents;
  using DredgeAI.BidCompare.Evidences;
  using Shouldly;
  using Volo.Abp;
  using Xunit;

  namespace DredgeAI.BidCompare.Reports;

  public class ReportTests : BidCompareApplicationTestBase
  {
      private readonly ICompareTaskAppService _appService;
      private readonly FakeLlmGateway _llmGateway;

      public ReportTests()
      {
          _appService = GetRequiredService<ICompareTaskAppService>();
          _llmGateway = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
      }

      private async Task<Guid> PrepareDoneTaskAsync()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "一期比标" });
          var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", new MemoryStream(new byte[] { 1 }));
          var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf", new MemoryStream(new byte[] { 2 }));
          var parseJob = GetRequiredService<ParseDocumentJob>();
          await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docA.Id });
          await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docB.Id });
          await _appService.ConfirmClausesAsync(task.Id, new ConfirmClausesInput
          {
              Clauses = new() { new ClauseInputDto { ClauseId = "c1", Text = "须提供 ISO9001 证书", Mandatory = true } }
          });
          _llmGateway.QueueResponse("""[{"clauseId":"c1","status":"none","reason":"未提供证书","blockIds":["b0001"]}]""");
          _llmGateway.QueueResponse("""[{"clauseId":"c1","status":"responded","reason":"已提供","blockIds":[]}]""");
          _llmGateway.QueueResponse("""[]""");
          await GetRequiredService<CompareDocumentsJob>().ExecuteAsync(new CompareDocumentsArgs { TaskId = task.Id });
          await GetRequiredService<AiAnalysisJob>().ExecuteAsync(new AiAnalysisArgs { TaskId = task.Id });
          return task.Id;
      }

      [Fact]
      public async Task Report_Should_Be_Assembled_And_Cached_After_Done()
      {
          var taskId = await PrepareDoneTaskAsync();

          var report = await _appService.GetReportAsync(taskId);

          report.TaskId.ShouldBe(taskId);
          report.GeneratedAt.ShouldBeGreaterThan(DateTime.MinValue);
          report.Summary.DocCount.ShouldBe(2);
          report.Summary.HighRiskCount.ShouldBe(1); // c1 未响应（mandatory）
          report.Summary.TopFindings.ShouldNotBeEmpty();
          report.Matrix.Cells.Count.ShouldBe(4);
          report.Sections.Select(s => s.Key).ShouldBe(
              new[] { "bidRiggingRisk", "clauseCompliance", "indicatorComparison" }, ignoreOrder: false);

          var clauseSection = report.Sections.Single(s => s.Key == "clauseCompliance");
          clauseSection.Title.ShouldBe("强制性条款响应");
          clauseSection.Evidences.Count.ShouldBe(1);
          clauseSection.Evidences[0].AiGenerated.ShouldBeTrue();

          // 缓存：二次读取反序列化自 CompareTask.ReportJson，结果一致
          var again = await _appService.GetReportAsync(taskId);
          again.GeneratedAt.ShouldBe(report.GeneratedAt);
          again.Summary.HighRiskCount.ShouldBe(1);
      }

      [Fact]
      public async Task Report_Before_Done_Should_Throw_ReportNotReady()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });

          var ex = await Should.ThrowAsync<BusinessException>(() => _appService.GetReportAsync(task.Id));
          ex.Code.ShouldBe(BidCompareErrorCodes.ReportNotReady);
      }
  }
  ```

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter ReportTests
  ```

  预期：**编译失败**（`GetReportAsync`/`ReportBuilder` 不存在）。

- [ ] **Step 3: 实现 ReportBuilder**

  创建 `src/DredgeAI.BidCompare.Application/Reporting/ReportBuilder.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.Analysis;
  using DredgeAI.BidCompare.Documents;
  using DredgeAI.BidCompare.Evidences;
  using DredgeAI.BidCompare.Reports;
  using Volo.Abp.DependencyInjection;
  using Volo.Abp.Domain.Repositories;
  using Volo.Abp.Linq;
  using Volo.Abp.Timing;

  namespace DredgeAI.BidCompare.Reporting;

  /// <summary>
  /// 报告 JSON 组装（spec §8）：摘要（高/中/低计数 + Top5）、相似度矩阵、
  /// 三节证据（围标风险 = similarity/pricing/metadata；条款响应 = clause；指标比选 = indicator）。
  /// 证据与结果工作台同源（同一 EvidenceItem）。
  /// </summary>
  public class ReportBuilder : ITransientDependency
  {
      private readonly IRepository<EvidenceItem, Guid> _evidenceRepository;
      private readonly IRepository<CompareDocument, Guid> _documentRepository;
      private readonly IAsyncQueryableExecuter _asyncExecuter;
      private readonly IClock _clock;

      public ReportBuilder(
          IRepository<EvidenceItem, Guid> evidenceRepository,
          IRepository<CompareDocument, Guid> documentRepository,
          IAsyncQueryableExecuter asyncExecuter,
          IClock clock)
      {
          _evidenceRepository = evidenceRepository;
          _documentRepository = documentRepository;
          _asyncExecuter = asyncExecuter;
          _clock = clock;
      }

      public async Task<CompareReportDto> BuildAsync(Guid taskId, CancellationToken cancellationToken = default)
      {
          var evQueryable = await _evidenceRepository.GetQueryableAsync();
          var evidences = (await _asyncExecuter.ToListAsync(evQueryable.Where(e => e.TaskId == taskId)))
              .Select(EvidenceMapper.ToDto)
              .OrderBy(e => e.Severity)
              .ThenBy(e => e.Title)
              .ToList();

          var docQueryable = await _documentRepository.GetQueryableAsync();
          var docCount = await _asyncExecuter.CountAsync(docQueryable.Where(d =>
              d.TaskId == taskId && d.Role == DocumentRole.Bid && d.ParseStatus == DocumentParseStatus.Parsed));

          return new CompareReportDto
          {
              TaskId = taskId,
              GeneratedAt = _clock.Now,
              Summary = new ReportSummaryDto
              {
                  DocCount = docCount,
                  HighRiskCount = evidences.Count(e => e.Severity == EvidenceSeverity.High),
                  MidRiskCount = evidences.Count(e => e.Severity == EvidenceSeverity.Mid),
                  LowRiskCount = evidences.Count(e => e.Severity == EvidenceSeverity.Low),
                  TopFindings = evidences.Take(5).Select(e => e.Title).ToList()
              },
              Matrix = await BuildMatrixAsync(taskId, evidences),
              Sections = new List<ReportSectionDto>
              {
                  new()
                  {
                      Key = "bidRiggingRisk",
                      Title = "围标风险",
                      Evidences = evidences.Where(e =>
                          e.Type is EvidenceType.Similarity or EvidenceType.Pricing or EvidenceType.Metadata).ToList()
                  },
                  new()
                  {
                      Key = "clauseCompliance",
                      Title = "强制性条款响应",
                      Evidences = evidences.Where(e => e.Type == EvidenceType.Clause).ToList()
                  },
                  new()
                  {
                      Key = "indicatorComparison",
                      Title = "关键指标比选",
                      Evidences = evidences.Where(e => e.Type == EvidenceType.Indicator).ToList()
                  }
              }
          };
      }

      private async Task<SimilarityMatrixDto> BuildMatrixAsync(Guid taskId, List<EvidenceDto> evidences)
      {
          var docQueryable = await _documentRepository.GetQueryableAsync();
          var docs = await _asyncExecuter.ToListAsync(docQueryable
              .Where(d => d.TaskId == taskId && d.Role == DocumentRole.Bid && d.ParseStatus == DocumentParseStatus.Parsed)
              .OrderBy(d => d.CreationTime));

          var similarityEvidences = evidences.Where(e => e.Type == EvidenceType.Similarity).ToList();
          var cells = new List<SimilarityMatrixCellDto>();
          foreach (var a in docs)
          {
              foreach (var b in docs)
              {
                  var similarity = a.Id == b.Id
                      ? 1.0
                      : similarityEvidences
                          .Where(e => e.Metrics?.Similarity != null && e.DocIds.Contains(a.Id) && e.DocIds.Contains(b.Id))
                          .Select(e => e.Metrics!.Similarity!.Value)
                          .DefaultIfEmpty(0.0)
                          .Max();
                  cells.Add(new SimilarityMatrixCellDto
                  {
                      DocAId = a.Id,
                      DocBId = b.Id,
                      Similarity = Math.Round(similarity, 4)
                  });
              }
          }

          return new SimilarityMatrixDto
          {
              DocIds = docs.Select(d => d.Id).ToList(),
              Cells = cells
          };
      }
  }
  ```

  > 注：`BuildMatrixAsync` 与 `GetMatrixAsync`（Task 9）逻辑一致（同数据源、同取 max 规则），刻意各自实现避免 AppService 与领域服务互相耦合。

- [ ] **Step 4: AppService 追加 GetReportAsync + Controller 追加 action**

  `ICompareTaskAppService` 追加（`using DredgeAI.BidCompare.Reports;`）：

  ```csharp
  Task<CompareReportDto> GetReportAsync(Guid id);
  ```

  `CompareTaskAppService`：构造函数追加注入 `Reporting.ReportBuilder reportBuilder` 并赋值 `_reportBuilder` 字段；类内追加：

  ```csharp
  public async Task<CompareReportDto> GetReportAsync(Guid id)
  {
      var task = await _taskRepository.GetAsync(id);

      if (task.ReportJson != null)
      {
          return JsonSerializer.Deserialize<CompareReportDto>(task.ReportJson, SnapshotJsonOptions)!;
      }
      if (task.Status != CompareTaskStatus.Done)
      {
          throw new BusinessException(BidCompareErrorCodes.ReportNotReady).WithData("taskId", id);
      }

      var report = await _reportBuilder.BuildAsync(id);
      task.SetReport(JsonSerializer.Serialize(report, SnapshotJsonOptions), Clock.Now);
      await _taskRepository.UpdateAsync(task, autoSave: true);
      return report;
  }
  ```

  （`using DredgeAI.BidCompare.Reports;` 一并追加；`Clock` 为 ApplicationService 基座属性。）

  `CompareTaskController` 追加 action：

  ```csharp
  /// <summary>GET /api/compare/tasks/{id}/report 结构化报告 JSON</summary>
  [HttpGet("{id}/report")]
  public Task<Reports.CompareReportDto> GetReportAsync(Guid id)
      => _appService.GetReportAsync(id);
  ```

- [ ] **Step 5: 跑测试确认通过并提交**

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter ReportTests
  ```

  预期：2 passed。

  ```bash
  git add backend/DredgeAI.BidCompare
  git commit -m "feat(backend): add report JSON assembly with GET report API"
  ```

---

## Task 14 【P2】Word/PDF 异步导出

覆盖 spec §6 路由：`POST /api/compare/tasks/{id}/export`；补充路由 `GET /api/compare/tasks/{id}/exports/{jobId}`（spec §6.2 导出异步化 + 轮询下载链接）。Word = OpenXML 基于 docx 模板填充；PDF = LibreOffice headless 转换。

> 范围说明（诚实裁剪）：spec §8-4 的「原文截图（带高亮框）、页码引用」需要按 bbox 渲染图片，复杂度高，本 Task 报告正文以文字+矩阵表+证据条目呈现（每条证据含标题/描述/严重度/AI 标注，与工作台同一证据 ID，满足 §8 一致性原则的 ID 对应部分）；bbox 截图渲染列入 Task 15 跟进项。

**Files:**
- Create: `src/DredgeAI.BidCompare.Domain/Exports/IPdfConverter.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/Exports/ExportRequestDto.cs`
- Create: `src/DredgeAI.BidCompare.Application.Contracts/Exports/ExportJobDto.cs`
- Create: `src/DredgeAI.BidCompare.Application/Reporting/IWordReportRenderer.cs`
- Create: `src/DredgeAI.BidCompare.Application/Reporting/ReportExportOptions.cs`
- Create: `src/DredgeAI.BidCompare.Application/Reporting/DocxReportTemplateBuilder.cs`
- Create: `src/DredgeAI.BidCompare.Application/Reporting/OpenXmlWordReportRenderer.cs`
- Create: `src/DredgeAI.BidCompare.Application/BackgroundJobs/ExportReportArgs.cs`
- Create: `src/DredgeAI.BidCompare.Application/BackgroundJobs/ExportReportJob.cs`
- Modify: `src/DredgeAI.BidCompare.Application.Contracts/CompareTasks/ICompareTaskAppService.cs`
- Modify: `src/DredgeAI.BidCompare.Application/CompareTasks/CompareTaskAppService.cs`
- Modify: `src/DredgeAI.BidCompare.HttpApi/Controllers/CompareTaskController.cs`
- Modify: `src/DredgeAI.BidCompare.Application/DredgeAI.BidCompare.Application.csproj`（加 DocumentFormat.OpenXml）
- Create: `src/DredgeAI.BidCompare.HttpApi.Host/Exports/LibreOfficeOptions.cs`
- Create: `src/DredgeAI.BidCompare.HttpApi.Host/Exports/LibreOfficePdfConverter.cs`
- Modify: `src/DredgeAI.BidCompare.HttpApi.Host/BidCompareHttpApiHostModule.cs`
- Modify: `src/DredgeAI.BidCompare.HttpApi.Host/appsettings.json`
- Test: `test/DredgeAI.BidCompare.TestBase/Fakes/FakePdfConverter.cs`
- Test: `test/DredgeAI.BidCompare.Application.Tests/Fakes/FakeWordReportRenderer.cs`
- Modify: `test/DredgeAI.BidCompare.Application.Tests/BidCompareApplicationTestModule.cs`
- Test: `test/DredgeAI.BidCompare.Application.Tests/Exports/ExportJobTests.cs`
- Test: `test/DredgeAI.BidCompare.Application.Tests/Reporting/OpenXmlWordReportRendererTests.cs`

**Steps:**

- [ ] **Step 1: 定义 IPdfConverter / IWordReportRenderer / Fake / DTO**

  创建 `src/DredgeAI.BidCompare.Domain/Exports/IPdfConverter.cs`：

  ```csharp
  using System.Threading;
  using System.Threading.Tasks;

  namespace DredgeAI.BidCompare.Exports;

  /// <summary>docx → pdf 转换抽象（生产：LibreOffice headless；测试：Fake）。</summary>
  public interface IPdfConverter
  {
      Task<byte[]> ConvertToPdfAsync(byte[] docxContent, CancellationToken cancellationToken = default);
  }
  ```

  创建 `src/DredgeAI.BidCompare.Application/Reporting/IWordReportRenderer.cs`：

  ```csharp
  using System.Threading;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.Reports;

  namespace DredgeAI.BidCompare.Reporting;

  /// <summary>Word 报告渲染（OpenXML 基于 docx 模板填充）。</summary>
  public interface IWordReportRenderer
  {
      Task<byte[]> RenderAsync(CompareReportDto report, string taskName, CancellationToken cancellationToken = default);
  }
  ```

  创建 `src/DredgeAI.BidCompare.Application.Contracts/Exports/ExportRequestDto.cs`：

  ```csharp
  using System.ComponentModel.DataAnnotations;

  namespace DredgeAI.BidCompare.Exports;

  /// <summary>spec §6：{ format: 'pdf'|'word' }（枚举整型：0=Pdf, 1=Word）。</summary>
  public class ExportRequestDto
  {
      [Required]
      public ExportFormat Format { get; set; }
  }
  ```

  创建 `src/DredgeAI.BidCompare.Application.Contracts/Exports/ExportJobDto.cs`：

  ```csharp
  using System;

  namespace DredgeAI.BidCompare.Exports;

  /// <summary>导出任务句柄（spec §6.2）：POST 返回后立即轮询 GetExportJobAsync 直至 downloadUrl 非空。</summary>
  public class ExportJobDto
  {
      public Guid JobId { get; set; }

      public Guid TaskId { get; set; }

      public ExportFormat Format { get; set; }

      public ExportJobStatus Status { get; set; }

      public string? DownloadUrl { get; set; }

      public string? Error { get; set; }
  }
  ```

  创建 `test/DredgeAI.BidCompare.TestBase/Fakes/FakePdfConverter.cs`：

  ```csharp
  using System.Threading;
  using System.Threading.Tasks;

  namespace DredgeAI.BidCompare.Exports;

  public class FakePdfConverter : IPdfConverter
  {
      public byte[]? LastDocx { get; private set; }

      public Task<byte[]> ConvertToPdfAsync(byte[] docxContent, CancellationToken cancellationToken = default)
      {
          LastDocx = docxContent;
          return Task.FromResult(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 fake-pdf-content"));
      }
  }
  ```

  创建 `test/DredgeAI.BidCompare.Application.Tests/Fakes/FakeWordReportRenderer.cs`：

  ```csharp
  using System.Threading;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.Reports;

  namespace DredgeAI.BidCompare.Reporting;

  public class FakeWordReportRenderer : IWordReportRenderer
  {
      public CompareReportDto? LastReport { get; private set; }

      public Task<byte[]> RenderAsync(CompareReportDto report, string taskName, CancellationToken cancellationToken = default)
      {
          LastReport = report;
          return Task.FromResult(System.Text.Encoding.ASCII.GetBytes("FAKE-DOCX-CONTENT"));
      }
  }
  ```

  在 `BidCompareApplicationTestModule.ConfigureServices` 标注处追加两行（`using DredgeAI.BidCompare.Exports;`、`using DredgeAI.BidCompare.Reporting;` 一并追加）：

  ```csharp
  context.Services.Replace(ServiceDescriptor.Singleton<IPdfConverter, FakePdfConverter>());
  context.Services.Replace(ServiceDescriptor.Singleton<IWordReportRenderer, FakeWordReportRenderer>());
  ```

- [ ] **Step 2: 写失败测试（Word/PDF 导出全链路 + 轮询下载链接 + 未 Done 拒绝）**

  创建 `test/DredgeAI.BidCompare.Application.Tests/Exports/ExportJobTests.cs`：

  ```csharp
  using System;
  using System.IO;
  using System.Linq;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.BackgroundJobs;
  using DredgeAI.BidCompare.Clauses;
  using DredgeAI.BidCompare.CompareTasks;
  using DredgeAI.BidCompare.Documents;
  using DredgeAI.BidCompare.Exports;
  using DredgeAI.BidCompare.Storage;
  using Shouldly;
  using Volo.Abp;
  using Xunit;

  namespace DredgeAI.BidCompare.Exports;

  public class ExportJobTests : BidCompareApplicationTestBase
  {
      private readonly ICompareTaskAppService _appService;
      private readonly InMemoryFileStorage _fileStorage;

      public ExportJobTests()
      {
          _appService = GetRequiredService<ICompareTaskAppService>();
          _fileStorage = (InMemoryFileStorage)GetRequiredService<IFileStorage>();
      }

      private async Task<Guid> PrepareDoneTaskAsync()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "一期比标" });
          var docA = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书A.pdf", new MemoryStream(new byte[] { 1 }));
          var docB = await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "标书B.pdf", new MemoryStream(new byte[] { 2 }));
          var parseJob = GetRequiredService<ParseDocumentJob>();
          await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docA.Id });
          await parseJob.ExecuteAsync(new ParseDocumentArgs { TaskId = task.Id, DocumentId = docB.Id });
          await GetRequiredService<CompareDocumentsJob>().ExecuteAsync(new CompareDocumentsArgs { TaskId = task.Id });
          await GetRequiredService<AiAnalysisJob>().ExecuteAsync(new AiAnalysisArgs { TaskId = task.Id });
          return task.Id;
      }

      [Fact]
      public async Task Word_Export_Should_Succeed_And_Return_DownloadUrl()
      {
          var taskId = await PrepareDoneTaskAsync();

          var handle = await _appService.RequestExportAsync(taskId, new ExportRequestDto { Format = ExportFormat.Word });
          handle.Status.ShouldBe(ExportJobStatus.Pending);
          handle.DownloadUrl.ShouldBeNull();

          await GetRequiredService<ExportReportJob>().ExecuteAsync(new ExportReportArgs { ExportJobId = handle.JobId });

          var result = await _appService.GetExportJobAsync(taskId, handle.JobId);
          result.Status.ShouldBe(ExportJobStatus.Succeeded);
          result.DownloadUrl.ShouldNotBeNullOrWhiteSpace(); // spec §6.2：轮询获取下载链接
          _fileStorage.Objects.Keys.ShouldContain(k => k.StartsWith($"compare/{taskId}/exports/{handle.JobId}") && k.EndsWith(".docx"));
      }

      [Fact]
      public async Task Pdf_Export_Should_Convert_Via_PdfConverter()
      {
          var taskId = await PrepareDoneTaskAsync();
          var handle = await _appService.RequestExportAsync(taskId, new ExportRequestDto { Format = ExportFormat.Pdf });

          await GetRequiredService<ExportReportJob>().ExecuteAsync(new ExportReportArgs { ExportJobId = handle.JobId });

          var result = await _appService.GetExportJobAsync(taskId, handle.JobId);
          result.Status.ShouldBe(ExportJobStatus.Succeeded);
          var key = _fileStorage.Objects.Keys.Single(k => k.Contains(handle.JobId.ToString()));
          key.ShouldEndWith(".pdf");
      }

      [Fact]
      public async Task Export_Before_Done_Should_Throw()
      {
          var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });

          var ex = await Should.ThrowAsync<BusinessException>(() =>
              _appService.RequestExportAsync(task.Id, new ExportRequestDto { Format = ExportFormat.Word }));
          ex.Code.ShouldBe(BidCompareErrorCodes.ReportNotReady);
      }

      [Fact]
      public async Task GetExportJob_Of_Other_Task_Should_Throw()
      {
          var taskId = await PrepareDoneTaskAsync();
          var handle = await _appService.RequestExportAsync(taskId, new ExportRequestDto { Format = ExportFormat.Word });

          var ex = await Should.ThrowAsync<BusinessException>(() =>
              _appService.GetExportJobAsync(Guid.NewGuid(), handle.JobId));
          ex.Code.ShouldBeOneOf(BidCompareErrorCodes.ExportJobNotFound, "Volo.Abp.Domain.Entities.EntityNotFoundException");
      }
  }
  ```

  > 第 4 个测试：随机 taskId 不存在会先触发 ABP `EntityNotFoundException`（404 语义）；为稳定起见把它改为先创建第二个任务再查询：
  > 将 `GetExportJobAsync(Guid.NewGuid(), handle.JobId)` 替换为以 `_appService.CreateAsync(...)` 新建任务的 Id，断言 `ex.Code.ShouldBe(BidCompareErrorCodes.ExportJobNotFound);`（取一行实现为准，推荐改法）。

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter ExportJobTests
  ```

  预期：**编译失败**（`RequestExportAsync`/`ExportReportJob` 不存在）。

- [ ] **Step 3: 实现 ExportReportJob + AppService/Controller 追加**

  创建 `src/DredgeAI.BidCompare.Application/BackgroundJobs/ExportReportArgs.cs`：

  ```csharp
  using System;

  namespace DredgeAI.BidCompare.BackgroundJobs;

  public class ExportReportArgs
  {
      public Guid ExportJobId { get; set; }
  }
  ```

  创建 `src/DredgeAI.BidCompare.Application/BackgroundJobs/ExportReportJob.cs`：

  ```csharp
  using System;
  using System.IO;
  using System.Threading;
  using System.Threading.Tasks;
  using DredgeAI.BidCompare.CompareTasks;
  using DredgeAI.BidCompare.Exports;
  using DredgeAI.BidCompare.Reporting;
  using DredgeAI.BidCompare.Storage;
  using Microsoft.Extensions.Logging;
  using Volo.Abp.BackgroundJobs;
  using Volo.Abp.DependencyInjection;
  using Volo.Abp.Domain.Repositories;

  namespace DredgeAI.BidCompare.BackgroundJobs;

  /// <summary>导出后台任务（spec §6.2 异步化）：报告 JSON → docx →（可选）pdf → 对象存储。</summary>
  public class ExportReportJob : AsyncBackgroundJob<ExportReportArgs>, ITransientDependency
  {
      private readonly IRepository<ExportJob, Guid> _exportJobRepository;
      private readonly IRepository<CompareTask, Guid> _taskRepository;
      private readonly ReportBuilder _reportBuilder;
      private readonly IWordReportRenderer _wordReportRenderer;
      private readonly IPdfConverter _pdfConverter;
      private readonly IFileStorage _fileStorage;

      public ExportReportJob(
          IRepository<ExportJob, Guid> exportJobRepository,
          IRepository<CompareTask, Guid> taskRepository,
          ReportBuilder reportBuilder,
          IWordReportRenderer wordReportRenderer,
          IPdfConverter pdfConverter,
          IFileStorage fileStorage)
      {
          _exportJobRepository = exportJobRepository;
          _taskRepository = taskRepository;
          _reportBuilder = reportBuilder;
          _wordReportRenderer = wordReportRenderer;
          _pdfConverter = pdfConverter;
          _fileStorage = fileStorage;
      }

      public override async Task ExecuteAsync(ExportReportArgs args, CancellationToken cancellationToken = default)
      {
          var job = await _exportJobRepository.FindAsync(args.ExportJobId, cancellationToken: cancellationToken);
          if (job == null)
          {
              Logger.LogWarning("ExportJob {ExportJobId} 不存在，跳过导出", args.ExportJobId);
              return;
          }

          job.MarkRunning();
          await _exportJobRepository.UpdateAsync(job, autoSave: true, cancellationToken: cancellationToken);

          try
          {
              var task = await _taskRepository.GetAsync(job.TaskId, cancellationToken: cancellationToken);
              var report = await _reportBuilder.BuildAsync(job.TaskId, cancellationToken);
              var docx = await _wordReportRenderer.RenderAsync(report, task.Name, cancellationToken);

              byte[] output;
              string extension;
              string contentType;
              if (job.Format == ExportFormat.Word)
              {
                  output = docx;
                  extension = "docx";
                  contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
              }
              else
              {
                  output = await _pdfConverter.ConvertToPdfAsync(docx, cancellationToken);
                  extension = "pdf";
                  contentType = "application/pdf";
              }

              var key = $"compare/{job.TaskId}/exports/{job.Id}.{extension}";
              await _fileStorage.UploadAsync(key, new MemoryStream(output), contentType, cancellationToken);
              job.MarkSucceeded(key);
          }
          catch (Exception ex) when (ex is not OperationCanceledException)
          {
              Logger.LogWarning(ex, "导出任务 {ExportJobId} 失败", args.ExportJobId);
              job.MarkFailed(ex.Message); // spec §9：导出失败可重试（重新 POST export 即可）
          }

          await _exportJobRepository.UpdateAsync(job, autoSave: true, cancellationToken: cancellationToken);
      }
  }
  ```

  `ICompareTaskAppService` 追加（`using DredgeAI.BidCompare.Exports;`）：

  ```csharp
  Task<ExportJobDto> RequestExportAsync(Guid id, ExportRequestDto input);

  Task<ExportJobDto> GetExportJobAsync(Guid id, Guid jobId);
  ```

  `CompareTaskAppService`：构造函数追加注入 `IRepository<ExportJob, Guid> exportJobRepository` 并赋值 `_exportJobRepository` 字段；类内追加：

  ```csharp
  public async Task<ExportJobDto> RequestExportAsync(Guid id, ExportRequestDto input)
  {
      var task = await _taskRepository.GetAsync(id);
      if (task.Status != CompareTaskStatus.Done)
      {
          throw new BusinessException(BidCompareErrorCodes.ReportNotReady).WithData("taskId", id);
      }

      var job = new ExportJob(GuidGenerator.Create(), id, input.Format);
      await _exportJobRepository.InsertAsync(job, autoSave: true);
      await _backgroundJobManager.EnqueueAsync(new ExportReportArgs { ExportJobId = job.Id });
      return MapToDto(job, downloadUrl: null);
  }

  public async Task<ExportJobDto> GetExportJobAsync(Guid id, Guid jobId)
  {
      await _taskRepository.GetAsync(id);
      var job = await _exportJobRepository.GetAsync(jobId);
      if (job.TaskId != id)
      {
          throw new BusinessException(BidCompareErrorCodes.ExportJobNotFound).WithData("jobId", jobId);
      }

      var downloadUrl = job.Status == ExportJobStatus.Succeeded && job.FileStorageKey != null
          ? await _fileStorage.GetPresignedUrlAsync(job.FileStorageKey, TimeSpan.FromHours(1))
          : null;
      return MapToDto(job, downloadUrl);
  }

  private static ExportJobDto MapToDto(ExportJob job, string? downloadUrl) => new()
  {
      JobId = job.Id,
      TaskId = job.TaskId,
      Format = job.Format,
      Status = job.Status,
      DownloadUrl = downloadUrl,
      Error = job.Error
  };
  ```

  （`using DredgeAI.BidCompare.Exports;` 一并追加。）

  `CompareTaskController` 追加 action：

  ```csharp
  /// <summary>POST /api/compare/tasks/{id}/export 生成导出文件 { format } → 异步 → 下载 URL</summary>
  [HttpPost("{id}/export")]
  public Task<Exports.ExportJobDto> RequestExportAsync(Guid id, [FromBody] Exports.ExportRequestDto input)
      => _appService.RequestExportAsync(id, input);

  /// <summary>GET /api/compare/tasks/{id}/exports/{jobId}（补充路由：导出轮询，spec §6.2）</summary>
  [HttpGet("{id}/exports/{jobId}")]
  public Task<Exports.ExportJobDto> GetExportJobAsync(Guid id, Guid jobId)
      => _appService.GetExportJobAsync(id, jobId);
  ```

- [ ] **Step 4: 实现 OpenXML 模板生成器与渲染器（Application 层，纯托管可单测）**

  ```bash
  dotnet add src/DredgeAI.BidCompare.Application package DocumentFormat.OpenXml
  ```

  创建 `src/DredgeAI.BidCompare.Application/Reporting/ReportExportOptions.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.Reporting;

  public class ReportExportOptions
  {
      /// <summary>
      /// docx 模板路径。首次使用时若不存在由 DocxReportTemplateBuilder 自动生成；
      /// 正式商务风格模板（spec §11 待决事项4）可直接替换该文件，占位符保持不变。
      /// </summary>
      public string TemplatePath { get; set; } = "Templates/compare-report-template.docx";
  }
  ```

  创建 `src/DredgeAI.BidCompare.Application/Reporting/DocxReportTemplateBuilder.cs`：

  ```csharp
  using System.IO;
  using DocumentFormat.OpenXml.Packaging;
  using DocumentFormat.OpenXml.Wordprocessing;

  namespace DredgeAI.BidCompare.Reporting;

  /// <summary>
  /// 生成报告 docx 模板（封面 + 摘要占位符）。占位符（每个独立成段，保证在单个 Run 内）：
  /// {{TaskName}} {{GeneratedAt}} {{Conclusion}} {{DocCount}} {{HighCount}} {{MidCount}} {{LowCount}}
  /// </summary>
  public static class DocxReportTemplateBuilder
  {
      public static string EnsureTemplate(string templatePath)
      {
          if (File.Exists(templatePath))
          {
              return templatePath;
          }

          var directory = Path.GetDirectoryName(Path.GetFullPath(templatePath));
          if (!string.IsNullOrEmpty(directory))
          {
              Directory.CreateDirectory(directory);
          }

          using (var document = WordprocessingDocument.Create(templatePath, WordprocessingDocumentType.Document))
          {
              var mainPart = document.AddMainDocumentPart();
              mainPart.Document = new Document(new Body());
              var body = mainPart.Document.Body!;

              body.Append(TemplateParagraph("比标分析报告", bold: true, fontSize: "44"));
              body.Append(TemplateParagraph("任务名称：{{TaskName}}"));
              body.Append(TemplateParagraph("生成时间：{{GeneratedAt}}"));
              body.Append(TemplateParagraph("总体结论：{{Conclusion}}"));
              body.Append(TemplateParagraph("标书份数：{{DocCount}}　高风险：{{HighCount}}　中风险：{{MidCount}}　低风险：{{LowCount}}"));
              body.Append(new SectionProperties());

              mainPart.Document.Save();
          }

          return templatePath;
      }

      private static Paragraph TemplateParagraph(string text, bool bold = false, string? fontSize = null)
      {
          var runProperties = new RunProperties();
          if (bold)
          {
              runProperties.Append(new Bold());
          }
          if (fontSize != null)
          {
              runProperties.Append(new FontSize { Val = fontSize });
          }
          var run = new Run(runProperties, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
          return new Paragraph(run);
      }
  }
  ```

  创建 `src/DredgeAI.BidCompare.Application/Reporting/OpenXmlWordReportRenderer.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using DocumentFormat.OpenXml;
  using DocumentFormat.OpenXml.Packaging;
  using DocumentFormat.OpenXml.Wordprocessing;
  using DredgeAI.BidCompare.Evidences;
  using DredgeAI.BidCompare.Reports;
  using Microsoft.Extensions.Options;
  using Volo.Abp.DependencyInjection;

  namespace DredgeAI.BidCompare.Reporting;

  /// <summary>OpenXML 报告渲染：模板占位符替换（封面/摘要）+ 追加矩阵表与三节证据（spec §8 结构）。</summary>
  public class OpenXmlWordReportRenderer : IWordReportRenderer, ITransientDependency
  {
      private readonly ReportExportOptions _options;

      public OpenXmlWordReportRenderer(IOptions<ReportExportOptions> options)
      {
          _options = options.Value;
      }

      public Task<byte[]> RenderAsync(CompareReportDto report, string taskName, CancellationToken cancellationToken = default)
      {
          var templatePath = DocxReportTemplateBuilder.EnsureTemplate(_options.TemplatePath);
          var templateBytes = File.ReadAllBytes(templatePath);

          using var stream = new MemoryStream();
          stream.Write(templateBytes, 0, templateBytes.Length);

          using (var document = WordprocessingDocument.Open(stream, true))
          {
              var body = document.MainDocumentPart!.Document.Body!;

              ReplaceTokens(body, new Dictionary<string, string>
              {
                  ["{{TaskName}}"] = taskName,
                  ["{{GeneratedAt}}"] = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                  ["{{Conclusion}}"] = BuildConclusion(report),
                  ["{{DocCount}}"] = report.Summary.DocCount.ToString(),
                  ["{{HighCount}}"] = report.Summary.HighRiskCount.ToString(),
                  ["{{MidCount}}"] = report.Summary.MidRiskCount.ToString(),
                  ["{{LowCount}}"] = report.Summary.LowRiskCount.ToString()
              });

              var sectPr = body.Elements<SectionProperties>().FirstOrDefault();
              void Append(OpenXmlElement element)
              {
                  if (sectPr != null)
                  {
                      body.InsertBefore(element, sectPr);
                  }
                  else
                  {
                      body.Append(element);
                  }
              }

              // spec §8-2 摘要
              Append(Heading1("一、摘要"));
              if (report.Summary.TopFindings.Count == 0)
              {
                  Append(Para("无重大发现。"));
              }
              foreach (var finding in report.Summary.TopFindings)
              {
                  Append(Para("• " + finding));
              }

              // spec §8-3 相似度矩阵
              Append(Heading1("二、相似度矩阵"));
              Append(BuildMatrixTable(report));

              // spec §8-4/5/6 三节详情
              var numerals = new[] { "三", "四", "五" };
              for (var i = 0; i < report.Sections.Count && i < numerals.Length; i++)
              {
                  var section = report.Sections[i];
                  Append(Heading1($"{numerals[i]}、{section.Title}"));
                  if (section.Evidences.Count == 0)
                  {
                      Append(Para("无。"));
                  }
                  foreach (var evidence in section.Evidences)
                  {
                      Append(Para($"【{SeverityText(evidence.Severity)}】{evidence.Title}", bold: true));
                      Append(Para(evidence.Description));
                      if (evidence.AiGenerated)
                      {
                          Append(Para("（AI 分析）")); // spec §8-4：AI 生成的判断标注「AI 分析」
                      }
                  }
              }

              // spec §8-7 附录
              Append(Heading1("六、附录"));
              Append(Para("条款清单快照与解析质量说明以系统内任务数据为准。"));
              Append(Para("免责声明：本报告由 AI 投标-比标系统自动生成，结论供评审参考，不构成最终评标依据。"));

              document.MainDocumentPart.Document.Save();
          }

          return Task.FromResult(stream.ToArray());
      }

      private static string BuildConclusion(CompareReportDto report)
      {
          if (report.Summary.HighRiskCount > 0)
          {
              return $"发现 {report.Summary.HighRiskCount} 项高风险问题，存在围串标嫌疑，建议重点核查。";
          }
          if (report.Summary.MidRiskCount > 0)
          {
              return $"未发现高风险问题；存在 {report.Summary.MidRiskCount} 项中风险事项，建议关注。";
          }
          return "未发现明显围串标嫌疑。";
      }

      private static string SeverityText(EvidenceSeverity severity) => severity switch
      {
          EvidenceSeverity.High => "高风险",
          EvidenceSeverity.Mid => "中风险",
          _ => "低风险"
      };

      private static void ReplaceTokens(Body body, Dictionary<string, string> tokens)
      {
          foreach (var text in body.Descendants<Text>())
          {
              foreach (var (token, value) in tokens)
              {
                  if (text.Text.Contains(token))
                  {
                      text.Text = text.Text.Replace(token, value);
                  }
              }
          }
      }

      private static Paragraph Heading1(string text)
      {
          var run = new Run(new RunProperties(new Bold(), new FontSize { Val = "32" }), new Text(text));
          return new Paragraph(run);
      }

      private static Paragraph Para(string text, bool bold = false)
      {
          var run = new Run();
          if (bold)
          {
              run.Append(new RunProperties(new Bold()));
          }
          run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
          return new Paragraph(run);
      }

      private static Table BuildMatrixTable(CompareReportDto report)
      {
          var table = new Table(
              new TableProperties(
                  new TableBorders(
                      new TopBorder { Val = BorderValues.Single, Size = 4 },
                      new BottomBorder { Val = BorderValues.Single, Size = 4 },
                      new LeftBorder { Val = BorderValues.Single, Size = 4 },
                      new RightBorder { Val = BorderValues.Single, Size = 4 },
                      new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                      new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

          var headerCells = new List<TableCell> { Cell("A\\B") };
          headerCells.AddRange(report.Matrix.DocIds.Select(id => Cell(ShortId(id))));
          table.Append(new TableRow(headerCells.ToArray()));

          foreach (var a in report.Matrix.DocIds)
          {
              var row = new List<TableCell> { Cell(ShortId(a)) };
              foreach (var b in report.Matrix.DocIds)
              {
                  var cell = report.Matrix.Cells.First(c => c.DocAId == a && c.DocBId == b);
                  row.Add(Cell(cell.Similarity.ToString("0.00")));
              }
              table.Append(new TableRow(row.ToArray()));
          }

          return table;
      }

      private static TableCell Cell(string text)
          => new(new Paragraph(new Run(new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve })));

      private static string ShortId(Guid id) => id.ToString("N")[..8];
  }
  ```

  在 `BidCompareHttpApiHostModule.ConfigureServices` 末尾追加：

  ```csharp
  Configure<ReportExportOptions>(context.Configuration.GetSection("Export"));
  ```

  （`using DredgeAI.BidCompare.Reporting;` 一并追加。）

- [ ] **Step 5: 写渲染器单元测试（真实 OpenXML，模板自动生成到临时目录）**

  创建 `test/DredgeAI.BidCompare.Application.Tests/Reporting/OpenXmlWordReportRendererTests.cs`：

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Threading.Tasks;
  using DocumentFormat.OpenXml.Packaging;
  using DocumentFormat.OpenXml.Wordprocessing;
  using DredgeAI.BidCompare.Analysis;
  using DredgeAI.BidCompare.Evidences;
  using DredgeAI.BidCompare.Reports;
  using Microsoft.Extensions.Options;
  using Shouldly;
  using Xunit;

  namespace DredgeAI.BidCompare.Reporting;

  public class OpenXmlWordReportRendererTests
  {
      [Fact]
      public async Task Render_Should_Produce_Valid_Docx_With_Tokens_Replaced()
      {
          var templatePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "template.docx");
          var renderer = new OpenXmlWordReportRenderer(
              Options.Create(new ReportExportOptions { TemplatePath = templatePath }));

          var docA = Guid.NewGuid();
          var docB = Guid.NewGuid();
          var report = new CompareReportDto
          {
              TaskId = Guid.NewGuid(),
              GeneratedAt = new DateTime(2026, 7, 29, 8, 0, 0, DateTimeKind.Utc),
              Summary = new ReportSummaryDto
              {
                  DocCount = 2,
                  HighRiskCount = 1,
                  MidRiskCount = 0,
                  LowRiskCount = 0,
                  TopFindings = new List<string> { "标书A与标书B大段雷同" }
              },
              Matrix = new SimilarityMatrixDto
              {
                  DocIds = new List<Guid> { docA, docB },
                  Cells = new List<SimilarityMatrixCellDto>
                  {
                      new() { DocAId = docA, DocBId = docA, Similarity = 1.0 },
                      new() { DocAId = docA, DocBId = docB, Similarity = 0.93 },
                      new() { DocAId = docB, DocBId = docA, Similarity = 0.93 },
                      new() { DocAId = docB, DocBId = docB, Similarity = 1.0 }
                  }
              },
              Sections = new List<ReportSectionDto>
              {
                  new()
                  {
                      Key = "bidRiggingRisk",
                      Title = "围标风险",
                      Evidences = new List<EvidenceDto>
                      {
                          new()
                          {
                              Id = Guid.NewGuid(), TaskId = Guid.NewGuid(),
                              Type = EvidenceType.Similarity, Severity = EvidenceSeverity.High,
                              Title = "标书A与标书B大段雷同", Description = "第三章相似度 0.93",
                              AiGenerated = false
                          }
                      }
                  }
              }
          };

          var bytes = await renderer.RenderAsync(report, "一期工程比标");

          bytes.Length.ShouldBeGreaterThan(100);
          bytes[0].ShouldBe((byte)'P'); // docx 即 zip，PK 头
          bytes[1].ShouldBe((byte)'K');

          using var document = WordprocessingDocument.Open(new MemoryStream(bytes), false);
          var text = string.Concat(document.MainDocumentPart!.Document.Body!
              .Descendants<Text>().Select(t => t.Text));
          text.ShouldContain("一期工程比标");
          text.ShouldContain("标书A与标书B大段雷同");
          text.ShouldContain("0.93");
          text.ShouldContain("围标风险");
          text.ShouldNotContain("{{TaskName}}");
          text.ShouldNotContain("{{Conclusion}}");
      }
  }
  ```

- [ ] **Step 6: 实现 LibreOfficePdfConverter（生产）**

  创建 `src/DredgeAI.BidCompare.HttpApi.Host/Exports/LibreOfficeOptions.cs`：

  ```csharp
  namespace DredgeAI.BidCompare.Exports;

  public class LibreOfficeOptions
  {
      /// <summary>soffice 可执行文件路径（PATH 中则直接 "soffice"）。</summary>
      public string SofficePath { get; set; } = "soffice";

      public int TimeoutSeconds { get; set; } = 180;
  }
  ```

  创建 `src/DredgeAI.BidCompare.HttpApi.Host/Exports/LibreOfficePdfConverter.cs`：

  ```csharp
  using System.Diagnostics;
  using System.IO;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.Extensions.Options;
  using Volo.Abp;
  using Volo.Abp.DependencyInjection;

  namespace DredgeAI.BidCompare.Exports;

  /// <summary>LibreOffice headless 转换：soffice --headless --convert-to pdf --outdir {tmp} report.docx。</summary>
  public class LibreOfficePdfConverter : IPdfConverter, ITransientDependency
  {
      private readonly LibreOfficeOptions _options;

      public LibreOfficePdfConverter(IOptions<LibreOfficeOptions> options)
      {
          _options = options.Value;
      }

      public async Task<byte[]> ConvertToPdfAsync(byte[] docxContent, CancellationToken cancellationToken = default)
      {
          var workDir = Path.Combine(Path.GetTempPath(), "bidcompare-export", System.Guid.NewGuid().ToString("N"));
          Directory.CreateDirectory(workDir);
          try
          {
              var docxPath = Path.Combine(workDir, "report.docx");
              await File.WriteAllBytesAsync(docxPath, docxContent, cancellationToken);

              var startInfo = new ProcessStartInfo
              {
                  FileName = _options.SofficePath,
                  Arguments = $"--headless --convert-to pdf --outdir \"{workDir}\" \"{docxPath}\"",
                  RedirectStandardOutput = true,
                  RedirectStandardError = true,
                  UseShellExecute = false,
                  CreateNoWindow = true
              };

              using var process = Process.Start(startInfo)
                  ?? throw new BusinessException(BidCompareErrorCodes.ExportFailed).WithData("reason", "无法启动 soffice");

              using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
              timeoutCts.CancelAfter(System.TimeSpan.FromSeconds(_options.TimeoutSeconds));
              await process.WaitForExitAsync(timeoutCts.Token);

              var pdfPath = Path.Combine(workDir, "report.pdf");
              if (process.ExitCode != 0 || !File.Exists(pdfPath))
              {
                  var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                  throw new BusinessException(BidCompareErrorCodes.ExportFailed)
                      .WithData("exitCode", process.ExitCode)
                      .WithData("stderr", stderr);
              }

              return await File.ReadAllBytesAsync(pdfPath, cancellationToken);
          }
          finally
          {
              try
              {
                  Directory.Delete(workDir, recursive: true);
              }
              catch (IOException)
              {
                  // 临时目录清理失败不影响导出结果
              }
          }
      }
  }
  ```

  在 `BidCompareHttpApiHostModule.ConfigureServices` 末尾追加：

  ```csharp
  Configure<LibreOfficeOptions>(context.Configuration.GetSection("LibreOffice"));
  ```

  `appsettings.json` 顶层追加：

  ```json
  "Export": {
    "TemplatePath": "Templates/compare-report-template.docx"
  },
  "LibreOffice": {
    "SofficePath": "soffice",
    "TimeoutSeconds": 180
  }
  ```

- [ ] **Step 7: 跑测试确认通过**

  ```bash
  dotnet test test/DredgeAI.BidCompare.Application.Tests --filter "ExportJobTests|OpenXmlWordReportRendererTests"
  dotnet build DredgeAI.BidCompare.sln
  ```

  预期：ExportJobTests 4 passed + OpenXmlWordReportRendererTests 1 passed；解决方案 build 0 error。

- [ ] **Step 8: 提交**

  ```bash
  git add backend/DredgeAI.BidCompare
  git commit -m "feat(backend): add async Word/PDF export with OpenXML template rendering and LibreOffice conversion"
  ```

---
