# 解析任务卡死根治（fail-fast + 看门狗 + 恢复加固）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 AnGIneer 解析停滞的文档在 `StallTimeout`（默认 20 分钟）后自动 resume 一次、仍无进展即 fail-fast 落为失败，并把卡死看门狗真正启用、启动恢复不再无限复活停滞文档，避免单个卡死任务独占唯一后台 worker 阻塞整个队列。

**Architecture:** 在 `DocumentParsePipeline.PollUntilFinishedAsync` 中增加“停滞指纹”（progress|stage|stageMessage）检测：连续 `StallTimeout`（默认 20 分钟，覆盖 MinerU/PoPo 单步 15 分钟的实测场景）无变化先 resume 一次，再等一个窗口仍无变化直接抛 `AnGineerParseFailed` 判失败；`ParseRecoveryService` 按 `ParseStartedAt` 年龄区分“近期可续跑”与“长期停滞直接标失败”；`StuckTaskWatchdogWorker` 补齐 DI 注册并在宿主启动时加入后台 worker 管理器。TDD：先写失败测试（用 `FakeAnGineerClient.RepeatingState` 模拟永远不推进的状态），再实现。

**Tech Stack:** .NET 8 / ABP 8.3 BackgroundJobs（单 worker 串行执行）/ xUnit + Shouldly / EF Core InMemory SQLite 测试库。

---

## 背景（问题证据）

2026-08-18 事故：任务 `3a232717-3c0f-…` 的两份文档 20:48 入队后一直 `pending`，原因是 ABP 后台 worker 一次只执行一个任务，它从 20:45:12 起被 `ParseRecoveryService` 复活的一个老文档解析任务（AnGIneer doc `v1-a71c8cc33911`，卡在 `raw_parse / 0% / "label 归一化"` 数小时）独占，`PollUntilFinishedAsync` 只会傻等 30 分钟轮询超时。`StuckTaskWatchdogWorker` 已写好但从未注册（宿主里是空 `if` 块）。

本计划解决后端侧全部可治根因；AnGIneer 服务侧任务级超时与多租户/并行执行列为后续计划（见文末）。

## 文件结构

| 文件 | 职责 |
|---|---|
| `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/BackgroundJobs/AnGineerPollOptions.cs` | 新增 `StallTimeout` 停滞判定阈值 |
| `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/appsettings.json` | AnGIneer 配置节新增 `StallTimeout: 00:20:00`、`Timeout: 01:00:00` |
| `backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.TestBase/Fakes/FakeAnGineerClient.cs` | 新增 `RepeatingState` 支持模拟永远不推进的轮询状态 |
| `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/BackgroundJobs/DocumentParsePipeline.cs` | 停滞指纹检测：resume 一次 → fail-fast |
| `backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/BackgroundJobs/ParseDocumentJobTests.cs` | 停滞 fail-fast 与计时重置测试 |
| `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/BackgroundJobs/ParseRecoveryService.cs` | 启动恢复按停滞年龄分流：近期续跑 / 长期停滞标失败 |
| `backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/BackgroundJobs/ParseRecoveryServiceTests.cs` | 长期停滞文档不再被复活 |
| `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/BackgroundJobs/StuckTaskWatchdogWorker.cs` | 补 `ITransientDependency` 使 DI 可解析 |
| `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/BidCompareHttpApiHostModule.cs` | 宿主启动时注册看门狗 worker |

---

### Task 1: 增加 `StallTimeout` 轮询停滞阈值

**Files:**
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/BackgroundJobs/AnGineerPollOptions.cs:6-9`
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/appsettings.json:39-44`

- [ ] **Step 1: 给 `AnGineerPollOptions` 增加 `StallTimeout`**

把 `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/BackgroundJobs/AnGineerPollOptions.cs` 改为：

```csharp
using System;

namespace DredgeAI.BidCompare.BackgroundJobs;

public class AnGineerPollOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>单次解析轮询总上限（MinerU/PoPo 等单步可达 15 分钟，整篇大标书可超 30 分钟）。</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(60);

    /// <summary>
    /// 停滞判定：processing 状态下 progress/stage/stageMessage 连续无变化的时长上限。
    /// 超时先 resume 一次，仍无进展则 fail-fast（默认 20 分钟，覆盖 MinerU/PoPo 单步 15 分钟的实测场景）。
    /// </summary>
    public TimeSpan StallTimeout { get; set; } = TimeSpan.FromMinutes(20);
}
```

- [ ] **Step 2: appsettings.json 加入配置**

把 `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/appsettings.json` 的 AnGIneer 节改为：

```json
  "AnGIneer": {
    "BaseUrl": "http://localhost:8790",
    "ApiKey": null,
    "PollInterval": "00:00:05",
    "Timeout": "01:00:00",
    "StallTimeout": "00:20:00"
  },
```

- [ ] **Step 3: 编译验证**

Run:
```
dotnet build backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/DredgeAI.BidCompare.Application.csproj
```
Expected: `Build succeeded`，无编译错误（`StallTimeout` 属性与 appsettings 反序列化均由 Task 3 的测试间接覆盖）。

- [ ] **Step 4: Commit**

```bash
git add backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/BackgroundJobs/AnGineerPollOptions.cs backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/appsettings.json
git commit -m "feat(compare): add stall timeout option for AnGIneer polling"
```

---

### Task 2: `FakeAnGineerClient` 支持“固定状态重复返回”

**Files:**
- Modify: `backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.TestBase/Fakes/FakeAnGineerClient.cs:35-90`（属性区）与 `:139-170`（`GetStateAsync` 尾部）

测试基建：没有独立的测试文件，由 Task 3 的测试直接使用。

- [ ] **Step 1: 增加 `RepeatingState` 属性**

在 `FakeAnGineerClient.cs` 的 `StateSequence` 属性下方（约第 38 行）加入：

```csharp
    /// <summary>轮询始终返回的固定状态（模拟 AnGIneer 任务停滞）；StateSequence 耗尽后生效。</summary>
    public AnGineerJobStatus? RepeatingState { get; set; }
```

- [ ] **Step 2: `GetStateAsync` 返回固定状态**

把 `GetStateAsync` 中 `StateSequence` 分支之后、默认成功返回之前的代码改为：

```csharp
        if (StateSequence is { IsEmpty: false } sequence)
        {
            if (sequence.TryDequeue(out var status))
            {
                return Task.FromResult(status);
            }
        }
        if (RepeatingState != null)
        {
            return Task.FromResult(RepeatingState);
        }
        return Task.FromResult(new AnGineerJobStatus(
            AnGineerJobState.Succeeded, 100, "completed", "解析结束: completed"));
```

- [ ] **Step 3: 编译验证**

Run:
```
dotnet build backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.TestBase/DredgeAI.BidCompare.TestBase.csproj
```
Expected: `Build succeeded`。

- [ ] **Step 4: Commit**

```bash
git add backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.TestBase/Fakes/FakeAnGineerClient.cs
git commit -m "test(compare): support repeating AnGIneer state in fake client"
```

---

### Task 3: 轮询停滞检测（resume 一次 → fail-fast）TDD

**Files:**
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/BackgroundJobs/DocumentParsePipeline.cs:127-204`
- Test: `backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/BackgroundJobs/ParseDocumentJobTests.cs`（文件末尾追加 2 个测试）

- [ ] **Step 1: 写失败测试**

在 `ParseDocumentJobTests.cs` 顶部 using 区增加 `using Microsoft.Extensions.Options;`（放在现有 `using Volo.Abp.BackgroundJobs;` 之前），然后在文件末尾追加：

```csharp
    [Fact]
    public async Task Stalled_Progress_Should_Resume_Once_Then_Fail_Fast()
    {
        // 复现线上卡死：raw_parse + 0% + 非空消息，progress/stage/message 长时间无变化。
        // 修复前会一直轮询到 30 分钟超时独占后台 worker；修复后 resume 一次仍无进展即 fail-fast。
        var pollOptions = GetRequiredService<IOptions<AnGineerPollOptions>>().Value;
        pollOptions.PollInterval = TimeSpan.FromMilliseconds(10);
        pollOptions.StallTimeout = TimeSpan.FromMilliseconds(100);

        _anGineerClient.RepeatingState = new AnGineerJobStatus(
            AnGineerJobState.Processing, 0, "raw_parse", "label 归一化");
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var docRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<CompareDocument, Guid>>();
        var failed = await docRepo.GetAsync(doc.Id);
        failed.ParseStatus.ShouldBe(DocumentParseStatus.Failed);
        failed.ParseError.ShouldContain("停滞");
        _anGineerClient.ResumeCount.ShouldBe(1); // 每个停滞期最多 resume 一次

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Failed);
    }

    [Fact]
    public async Task Progress_Change_Should_Reset_Stall_Timer()
    {
        // 总耗时超过 StallTimeout，但每次轮询 signature 都推进 → 不应被判停滞
        var pollOptions = GetRequiredService<IOptions<AnGineerPollOptions>>().Value;
        pollOptions.PollInterval = TimeSpan.FromMilliseconds(50);
        pollOptions.StallTimeout = TimeSpan.FromMilliseconds(100);

        _anGineerClient.StateSequence = new ConcurrentQueue<AnGineerJobStatus>(new[]
        {
            new AnGineerJobStatus(AnGineerJobState.Processing, 0, "raw_parse", "label 归一化"),
            new AnGineerJobStatus(AnGineerJobState.Processing, 0, "raw_parse", "label 归一化"),
            new AnGineerJobStatus(AnGineerJobState.Processing, 10, "raw_parse", "label 归一化"),
            new AnGineerJobStatus(AnGineerJobState.Processing, 20, "raw_parse", "label 归一化"),
            new AnGineerJobStatus(AnGineerJobState.Succeeded, 100, "completed", "解析结束: completed")
        });
        var (task, doc) = await CreateTaskWithBidDocAsync();

        await RunParseJobAsync(task.Id, doc.Id);

        var detail = await _appService.GetAsync(task.Id);
        detail.Status.ShouldBe(CompareTaskStatus.Parsed);
    }
```

- [ ] **Step 2: 运行测试确认失败**

Run:
```
dotnet test backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/DredgeAI.BidCompare.Application.Tests.csproj --filter "FullyQualifiedName~Stalled_Progress_Should_Resume_Once_Then_Fail_Fast"
```
Expected: FAIL —— 当前实现没有停滞检测，`RepeatingState` 会一直轮询到 30 分钟 `Timeout` 才抛“轮询超时”，测试在 `ResumeCount.ShouldBe(1)` 处失败（实际 resume 0 次）。

（`Progress_Change_Should_Reset_Stall_Timer` 在旧实现下恰好能通过，属于防回归测试；不必单独先跑失败。）

- [ ] **Step 3: 实现停滞检测**

把 `DocumentParsePipeline.cs` 的 `PollUntilFinishedAsync`（127-204 行）整体替换为：

```csharp
    public async Task<AnGineerJobStatus> PollUntilFinishedAsync(
        string anGineerJobId,
        CompareDocument document,
        SemaphoreSlim? writeGate,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + _pollOptions.Timeout;
        var staleResumeAttempted = false;
        var interruptedResumeAttempted = false;
        var stallResumeAttempted = false;
        string? lastSignature = null;
        DateTime? lastSignatureChangeAt = null;
        while (DateTime.UtcNow < deadline)
        {
            AnGineerJobStatus status;
            try
            {
                status = await _anGineerClient.GetStateAsync(anGineerJobId, cancellationToken);
            }
            catch (HttpRequestException ex) when (IsTransientHttpError(ex))
            {
                // AnGIneer 侧 keep-alive 连接被关闭导致复用旧连接收到 RST 等瞬时错误；
                // 不应把整篇文档判失败，稍后重试即可。
                _logger.LogWarning(ex, "AnGIneer 状态轮询瞬时失败，稍后重试: {JobId}", anGineerJobId);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                continue;
            }

            if (ShouldResumeStaleRecord(status) && !staleResumeAttempted)
            {
                _logger.LogWarning(
                    "AnGIneer 文档 {JobId} 疑似旧记录（progress=0 + 空阶段消息），尝试 resume 恢复",
                    anGineerJobId);
                await _anGineerClient.ResumeAsync(anGineerJobId, cancellationToken);
                staleResumeAttempted = true;
                stallResumeAttempted = true;
                ResetStallTracking(status, ref lastSignature, ref lastSignatureChangeAt);
                continue;
            }

            if (status.State == AnGineerJobState.Failed
                && !interruptedResumeAttempted
                && IsInterruptionError(status))
            {
                _logger.LogWarning(
                    "AnGIneer 文档 {JobId} 解析中断（{Message}），尝试 resume 恢复",
                    anGineerJobId, status.FailureReason);
                interruptedResumeAttempted = true;
                stallResumeAttempted = true;
                await _anGineerClient.ResumeAsync(anGineerJobId, cancellationToken);
                ResetStallTracking(status, ref lastSignature, ref lastSignatureChangeAt);
                continue;
            }

            // 停滞检测：processing 状态连续 StallTimeout 无任何变化 → resume 一次 → 仍无变化直接 fail-fast，
            // 避免长时间占用唯一后台 worker 阻塞整条队列（2026-08-18 线上事故根因）。
            if (status.State == AnGineerJobState.Processing)
            {
                var signature = BuildProgressSignature(status);
                if (signature != lastSignature)
                {
                    lastSignature = signature;
                    lastSignatureChangeAt = DateTime.UtcNow;
                    stallResumeAttempted = false;
                }
                else if (lastSignatureChangeAt != null
                         && DateTime.UtcNow - lastSignatureChangeAt.Value >= _pollOptions.StallTimeout)
                {
                    if (!stallResumeAttempted)
                    {
                        _logger.LogWarning(
                            "AnGIneer 文档 {JobId} 解析停滞（{Signature} 在 {Minutes} 分钟内无变化），尝试 resume 恢复",
                            anGineerJobId, signature, _pollOptions.StallTimeout.TotalMinutes);
                        await _anGineerClient.ResumeAsync(anGineerJobId, cancellationToken);
                        stallResumeAttempted = true;
                        lastSignatureChangeAt = DateTime.UtcNow;
                        continue;
                    }
                    throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
                        .WithData("reason",
                            $"AnGIneer 解析停滞（{signature} 在 {_pollOptions.StallTimeout.TotalMinutes:0.#} 分钟内无变化，resume 后仍无进展）");
                }
            }

            await PersistProgressAsync(document, status, writeGate, cancellationToken);
            if (status.State != AnGineerJobState.Processing)
            {
                return status;
            }
            await Task.Delay(_pollOptions.PollInterval, cancellationToken);
        }
        throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
            .WithData("reason", "轮询超时");
    }
```

再在 `ShouldResumeStaleRecord` 方法（约 325 行）上方新增两个私有辅助方法：

```csharp
    /// <summary>以 progress|stage|stageMessage 作为停滞指纹。</summary>
    private static string BuildProgressSignature(AnGineerJobStatus status)
        => $"{status.Progress}|{status.Stage}|{status.StageMessage}";

    /// <summary>resume 后重置停滞计时，并把当前状态作为新的停滞起点。</summary>
    private static void ResetStallTracking(
        AnGineerJobStatus status,
        ref string? lastSignature,
        ref DateTime? lastSignatureChangeAt)
    {
        lastSignature = BuildProgressSignature(status);
        lastSignatureChangeAt = DateTime.UtcNow;
    }
```

- [ ] **Step 4: 运行新增测试确认通过**

Run:
```
dotnet test backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/DredgeAI.BidCompare.Application.Tests.csproj --filter "FullyQualifiedName~Stalled_Progress_Should_Resume_Once_Then_Fail_Fast"
```
Expected: PASS，耗时约 0.5 秒。

Run:
```
dotnet test backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/DredgeAI.BidCompare.Application.Tests.csproj --filter "FullyQualifiedName~Progress_Change_Should_Reset_Stall_Timer"
```
Expected: PASS。

- [ ] **Step 5: 回归全部解析作业测试**

Run:
```
dotnet test backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/DredgeAI.BidCompare.Application.Tests.csproj --filter "FullyQualifiedName~ParseDocumentJobTests"
```
Expected: 全部 PASS（含 `Stale_Processing_Record_Should_Resume_And_Recover`、`Live_Processing_Stage_Should_Not_Be_Treated_As_Stale`、`Interrupted_Failure_Should_Auto_Resume_And_Recover` 等既有用例，停滞检测不应改变它们的行为）。

- [ ] **Step 6: Commit**

```bash
git add backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/BackgroundJobs/DocumentParsePipeline.cs backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/BackgroundJobs/ParseDocumentJobTests.cs
git commit -m "fix(compare): fail fast when AnGIneer parse progress stalls"
```

---

### Task 4: 启动恢复不再复活长期停滞文档 TDD

**Files:**
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/BackgroundJobs/ParseRecoveryService.cs:17-60`
- Test: `backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/BackgroundJobs/ParseRecoveryServiceTests.cs`（文件末尾追加 1 个测试）

- [ ] **Step 1: 写失败测试**

在 `ParseRecoveryServiceTests.cs` 末尾追加：

```csharp
    [Fact]
    public async Task Recover_Should_Mark_Old_Stuck_Docs_Failed_Instead_Of_Requeue()
    {
        // 线上事故：11:35 卡死的文档被 20:44 重启的恢复逻辑再次入队，复活后继续卡 30 分钟。
        // 修复后：ParseStartedAt 早于 DocumentParsingTimeout（35 分钟）的一律直接标失败，不再入队。
        var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
        var doc = await _appService.UploadDocumentAsync(
            task.Id, DocumentRole.Bid, "标书C.pdf",
            new MemoryStream(Encoding.UTF8.GetBytes("%PDF fake")));
        var docRepo = GetRequiredService<IRepository<CompareDocument, Guid>>();
        await WithUnitOfWorkAsync(async () =>
        {
            var entity = await docRepo.GetAsync(doc.Id);
            entity.MarkParsing();
            entity.SetAnGineerDocId("stuck-angineer-doc");
            await docRepo.UpdateAsync(entity, autoSave: true);
        });
        var startedAt = (await docRepo.GetAsync(doc.Id)).ParseStartedAt!.Value;
        _jobManager.Clear();

        var service = GetRequiredService<ParseRecoveryService>();
        await service.RecoverAsync(startedAt.AddMinutes(40)); // 已停滞 40 分钟，超过 35 分钟阈值

        _jobManager.LastEnqueued<ParseDocumentArgs>().ShouldBeNull();
        var failed = await docRepo.GetAsync(doc.Id);
        failed.ParseStatus.ShouldBe(DocumentParseStatus.Failed);
        failed.ParseError.ShouldContain("停滞");
    }
```

- [ ] **Step 2: 运行测试确认失败**

Run:
```
dotnet test backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/DredgeAI.BidCompare.Application.Tests.csproj --filter "FullyQualifiedName~Recover_Should_Mark_Old_Stuck_Docs_Failed_Instead_Of_Requeue"
```
Expected: FAIL —— 当前 `RecoverAsync` 无条件把该文档重新入队，`LastEnqueued<ParseDocumentArgs>()` 不为 null。

- [ ] **Step 3: 实现恢复加固**

把 `ParseRecoveryService.cs` 整体替换为：

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// 进程重启恢复：把仍处于 Parsing 且已有 AnGIneer doc_id 的文档重新入队续跑。
/// ParseDocumentJob 会先查 AnGIneer 状态，processing/failed 时调 resume，避免重新上传文件。
/// 仅恢复“近期”启动的解析（ParseStartedAt 在 DocumentParsingTimeout 内）；
/// 长期停滞的文档直接标记失败，避免重启反复复活同一卡死任务（2026-08-18 事故）。
/// </summary>
public class ParseRecoveryService : ITransientDependency
{
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly WatchdogOptions _watchdogOptions;
    private readonly ILogger<ParseRecoveryService> _logger;

    public ParseRecoveryService(
        IRepository<CompareDocument, Guid> documentRepository,
        IBackgroundJobManager backgroundJobManager,
        IOptions<WatchdogOptions> watchdogOptions,
        ILogger<ParseRecoveryService> logger)
    {
        _documentRepository = documentRepository;
        _backgroundJobManager = backgroundJobManager;
        _watchdogOptions = watchdogOptions.Value;
        _logger = logger;
    }

    public Task RecoverAsync(CancellationToken cancellationToken = default)
        => RecoverAsync(DateTime.UtcNow, cancellationToken);

    /// <summary>now 参数供测试注入固定时间点。</summary>
    public async Task RecoverAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var documents = await _documentRepository.GetListAsync(
            d => d.ParseStatus == DocumentParseStatus.Parsing && d.AnGineerDocId != null,
            cancellationToken: cancellationToken);
        var targets = documents
            .Where(d => !string.IsNullOrWhiteSpace(d.AnGineerDocId))
            .ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var deadline = now - _watchdogOptions.DocumentParsingTimeout;
        var hopeless = targets
            .Where(d => d.ParseStartedAt == null || d.ParseStartedAt.Value < deadline)
            .ToList();
        foreach (var document in hopeless)
        {
            var reason =
                $"启动恢复：解析自 {document.ParseStartedAt:O} 起已超过 {_watchdogOptions.DocumentParsingTimeout.TotalMinutes} 分钟仍无终态，按停滞处理";
            _logger.LogWarning("文档 {DocumentId} {Reason}，直接标记失败（不再入队）", document.Id, reason);
            document.MarkParseFailed(reason);
            await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
        }

        var resumable = targets
            .Where(d => d.ParseStartedAt != null && d.ParseStartedAt.Value >= deadline)
            .ToList();
        if (resumable.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "启动恢复：发现 {Count} 个解析中且已有 AnGIneer doc_id 的近期文档，重新入队续跑",
            resumable.Count);
        foreach (var document in resumable)
        {
            await _backgroundJobManager.EnqueueAsync(new ParseDocumentArgs
            {
                TaskId = document.TaskId,
                DocumentId = document.Id
            });
        }
    }
}
```

- [ ] **Step 4: 运行新增测试确认通过**

Run:
```
dotnet test backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/DredgeAI.BidCompare.Application.Tests.csproj --filter "FullyQualifiedName~Recover_Should_Mark_Old_Stuck_Docs_Failed_Instead_Of_Requeue"
```
Expected: PASS。

- [ ] **Step 5: 回归恢复测试（近期文档仍可续跑）**

Run:
```
dotnet test backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/DredgeAI.BidCompare.Application.Tests.csproj --filter "FullyQualifiedName~ParseRecoveryServiceTests"
```
Expected: 全部 PASS（既有 `Recover_Should_Requeue_Parsing_Docs_With_AnGineer_DocId` 刚 MarkParsing 的文档在窗口内，仍会入队）。

- [ ] **Step 6: Commit**

```bash
git add backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/BackgroundJobs/ParseRecoveryService.cs backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/BackgroundJobs/ParseRecoveryServiceTests.cs
git commit -m "fix(compare): stop startup recovery from resurrecting hopeless parse jobs"
```

---

### Task 5: 注册并启用卡死看门狗

**Files:**
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/BackgroundJobs/StuckTaskWatchdogWorker.cs:23`
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/BidCompareHttpApiHostModule.cs:374-378`

- [ ] **Step 1: 让看门狗可被 DI 解析**

在该文件顶部 using 区（`using Volo.Abp.BackgroundJobs;` 之前）增加：

```csharp
using Volo.Abp.DependencyInjection;
```

把 `StuckTaskWatchdogWorker.cs` 的类声明改为：

```csharp
public class StuckTaskWatchdogWorker : AsyncPeriodicBackgroundWorkerBase, ITransientDependency
```

（`AsyncPeriodicBackgroundWorkerBase` 本身不实现 `ITransientDependency`，必须显式声明才能被 `AddBackgroundWorkerAsync<T>` 的 `GetRequiredService<T>()` 解析。）

- [ ] **Step 2: 宿主启动时注册看门狗**

把 `BidCompareHttpApiHostModule.cs` 的 `OnApplicationInitializationAsync` 中现有空块：

```csharp
        var watchdog = context.ServiceProvider.GetRequiredService<IOptions<WatchdogOptions>>().Value;
        if (watchdog.Enabled)
        {
        // 卡死看门狗（M9）：Parsing/Comparing/Analyzing 中间态超时巡检
        }
```

替换为：

```csharp
        var watchdog = context.ServiceProvider.GetRequiredService<IOptions<WatchdogOptions>>().Value;
        if (watchdog.Enabled)
        {
            // 卡死看门狗（M9）：Parsing/Comparing/Analyzing 中间态超时巡检
            await context.AddBackgroundWorkerAsync<StuckTaskWatchdogWorker>();
        }
```

（`AddBackgroundWorkerAsync<T>` 扩展方法位于 `Volo.Abp.BackgroundWorkers`，文件已 `using`。）

- [ ] **Step 3: 编译宿主**

Run:
```
dotnet build backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/DredgeAI.BidCompare.HttpApi.Host.csproj
```
Expected: `Build succeeded`。

- [ ] **Step 4: 冒烟启动验证注册生效**

Run（本机 44361 被占用时先停掉旧进程，见 Task 6 Step 1）：
```
dotnet run --project backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/DredgeAI.BidCompare.HttpApi.Host.csproj
```
Expected: 日志出现
`Started background worker: DredgeAI.BidCompare.BackgroundJobs.StuckTaskWatchdogWorker`
随后 `Now listening on: https://localhost:44361`。Ctrl+C 停止。

- [ ] **Step 5: Commit**

```bash
git add backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/BackgroundJobs/StuckTaskWatchdogWorker.cs backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/BidCompareHttpApiHostModule.cs
git commit -m "fix(compare): enable stuck-task watchdog worker"
```

---

### Task 6: 清理当前卡死任务并端到端验证（运维）

**Files:** 无（只操作数据库与进程）。

> 注意：当前 git 工作区已有与本次无关的 `user-web/src/router/manifests.ts`、`user-web/src/views/ai-bid/compare/*` 改动，全程不要 `git add -A`，只 add 本计划涉及的 backend 文件。

- [ ] **Step 1: 停止旧后端进程**

Run:
```
Get-Process DredgeAI.BidCompare.HttpApi.Host -ErrorAction SilentlyContinue | Stop-Process -Force
```
Expected: 44361 不再监听（`Get-NetTCPConnection -LocalPort 44361` 无结果）。

- [ ] **Step 2: 抛弃卡死文档的遗留解析作业**

Run（Postgres 在 docker 容器 `bidcompare-postgres` 中）：
```
docker exec bidcompare-postgres psql -U postgres -d BidCompare -c "UPDATE \"AbpBackgroundJobs\" SET \"IsAbandoned\" = true WHERE \"JobName\" = 'DredgeAI.BidCompare.BackgroundJobs.ParseDocumentArgs' AND \"IsAbandoned\" = false AND \"JobArgs\" LIKE '%3a23251d-4edd-f473-4b04-9b75e491c653%';"
```
Expected: `UPDATE 1` 或 `UPDATE 2`。

保留新任务 `3a232717-3c0f-df2f-9c64-1790e0b2c6ef` 的 `ParseDocumentsArgs` 作业（不清理），部署后由 worker 执行。

- [ ] **Step 3: 启动后端并观察恢复日志**

Run:
```
dotnet run --project backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/DredgeAI.BidCompare.HttpApi.Host.csproj
```
Expected: 启动日志出现
`文档 3a23251d-4edd-f473-4b04-9b75e491c653 启动恢复：解析自 … 起已超过 35 分钟仍无终态，按停滞处理`（旧卡死文档直接标失败，不再入队）。

- [ ] **Step 4: 验证两份等待文档开始解析并完成**

等待 worker 执行 `ParseDocumentsArgs` 后查询：
```
docker exec bidcompare-postgres psql -U postgres -d BidCompare -c "SELECT \"FileName\", \"ParseStatus\", \"ParseError\" FROM \"BcCompareDocuments\" WHERE \"TaskId\" = '3a232717-3c0f-df2f-9c64-1790e0b2c6ef';"
```
Expected: 两行 `ParseStatus` 从 `0`（pending）变为 `1`（parsing）再变为 `2`（parsed），`ParseError` 为空。

- [ ] **Step 5: 确认看门狗与恢复日志无异常**

在 `data/logs/backend.log` 中检索：
```
rg -n "StuckTaskWatchdogWorker|启动恢复|解析停滞|轮询超时" data/logs/backend.log
```
Expected: 出现看门狗启动行与恢复日志，无“轮询超时”阻塞新任务的记录。

- [ ] **Step 6: 全量测试收尾**

Run:
```
dotnet test backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/DredgeAI.BidCompare.Application.Tests.csproj
```
Expected: 全部 PASS。

---

## 后续计划（不在本计划范围）

1. **AnGIneer 服务侧根治**：docs-api（localhost:8790）对 `raw_parse` 等阶段长时间不推进的任务应有任务级超时/失败机制，避免上游任务永远挂起。
2. **多租户准备**：`ParseDocumentArgs` / `ParseDocumentsArgs` 目前未实现 `IMultiTenant`，ABP 8.3 的 `BackgroundJobInfo` 也不持久化 TenantId；做 SaaS 多租户时需在 args 中携带 TenantId（参考 ABP `BackgroundJobExecuter.GetJobArgsTenantId`）。
3. **并行执行**：ABP 8.3 默认 worker 单任务串行；若需吞吐，升级 ABP 10.x 使用 `MaxParallelJobExecutionCount` / dedicated worker，或替换为 Hangfire / Quartz（支持并行与按租户分队列）。
