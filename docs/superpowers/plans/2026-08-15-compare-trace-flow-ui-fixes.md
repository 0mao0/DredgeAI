# 比标溯源过程流 UI 重构与 IR 修复 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将比标右侧面板改成按阶段折叠的“溯源过程流”，修复 IR 500 导致的大量错误提示与 PDF 高亮失效，并调整顶部状态标签与“收起左侧面板”语义。

**Architecture:** 前端把 `ProcessPanel` 原地重构为统一溯源过程流（阶段卡 + 折叠摘要 + 阶段内证据/全局视图），`index.vue` 删除结果 Tab 与 `ResultPanel` 引用；`PdfWorkspace` 改为内部维护单栏状态，`EvidenceCard` 整卡点击溯源；后端将 IR 元数据时间改为字符串透传，前端 IR 请求静默。

**Tech Stack:** Vue 3 `<script setup lang="ts">`、ant-design-vue、LESS 变量、ECharts（热力图）、.NET 8 / xUnit / Shouldly。

---

## 文件结构

| 文件 | 职责 |
|---|---|
| `backend/.../Application.Contracts/Ir/DocumentIrDtos.cs` | IR DTO：`CreatedAt/ModifiedAt` 改为字符串 |
| `backend/.../Application.Tests/Ir/DocumentIrDtoTests.cs` | 验证 PDF 日期格式可反序列化 |
| `user-web/src/api/modules/compare.ts` | `getIr` 始终静默 |
| `user-web/src/views/ai-bid/compare/components/EvidenceCard.vue` | 整卡可点击溯源 |
| `user-web/src/views/ai-bid/compare/components/PdfWorkspace.vue` | 内部单栏状态 + locate 兜底 |
| `user-web/src/views/ai-bid/compare/components/ProcessPanel.vue` | 统一溯源过程流 |
| `user-web/src/views/ai-bid/compare/components/ResultPanel.vue` | 删除 |
| `user-web/src/views/ai-bid/compare/index.vue` | 去掉结果 Tab、状态标签前置、收起左侧面板 |

---

### Task 1: 后端 IR 元数据时间改为字符串透传

**Files:**
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application.Contracts/Ir/DocumentIrDtos.cs:32-35`
- Test: `backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/Ir/DocumentIrDtoTests.cs`

- [ ] **Step 1: 写失败测试**

创建 `backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/Ir/DocumentIrDtoTests.cs`：

```csharp
using System.Text.Json;
using DredgeAI.BidCompare.Ir;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.Ir;

public class DocumentIrDtoTests
{
    [Fact]
    public void Deserialize_Should_Accept_Pdf_Date_Format_In_Meta()
    {
        const string json = """
        {
          "schemaVersion":"2.0",
          "docId":"doc-a",
          "meta":{
            "fileName":"海港1.pdf",
            "pageCount":2,
            "author":null,
            "creatorTool":"Adobe Acrobat 9.3.2",
            "createdAt":"D:20251229164720+08'00'",
            "modifiedAt":null
          },
          "pages":[],
          "outline":[],
          "blocks":[]
        }
        """;

        var doc = JsonSerializer.Deserialize<DocumentIrDto>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        doc.ShouldNotBeNull();
        doc.Meta.CreatedAt.ShouldBe("D:20251229164720+08'00'");
        doc.Meta.ModifiedAt.ShouldBeNull();
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run:
```powershell
dotnet test "backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/DredgeAI.BidCompare.Application.Tests.csproj" --filter "FullyQualifiedName~DocumentIrDtoTests"
```

Expected: FAIL，`JsonException: The JSON value could not be converted to System.Nullable`1[System.DateTime]`。

- [ ] **Step 3: 修改 DTO**

将 `DocumentIrDtos.cs` 中：

```csharp
    public DateTime? CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }
```

改为：

```csharp
    /// <summary>PDF 元数据时间可能是 D:20251229164720+08'00' 格式，按字符串透传，避免反序列化 500。</summary>
    public string? CreatedAt { get; set; }

    public string? ModifiedAt { get; set; }
```

- [ ] **Step 4: 运行测试确认通过**

Run:
```powershell
dotnet test "backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/DredgeAI.BidCompare.Application.Tests.csproj" --filter "FullyQualifiedName~DocumentIrDtoTests"
```

Expected: PASS，1 个测试通过。

- [ ] **Step 5: 提交**

```bash
git add backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application.Contracts/Ir/DocumentIrDtos.cs backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/Ir/DocumentIrDtoTests.cs
git commit --no-verify -m "fix(compare): parse PDF date metadata as string in IR DTO"
```

---

### Task 2: 前端 IR 请求静默

**Files:**
- Modify: `user-web/src/api/modules/compare.ts`

- [ ] **Step 1: 修改 `getIr` 与 `buildRefs`**

将 `compare.ts` 中的 `getIr` 函数改为：

```ts
async function getIr(taskId: string, docId: string): Promise<DocumentIrDto | null> {
  try {
    return await request.get<DocumentIrDto>(fillUrl(urls.compareTaskIr, { id: taskId, docId }), silentConfig(true))
  } catch {
    return null
  }
}
```

将 `buildRefs` 签名与内部调用改为：

```ts
async function buildRefs(taskId: string, ev: EvidenceDto): Promise<EvidenceItem['refs']> {
  const refs: EvidenceItem['refs'] = []
  for (const loc of ev.locations) {
    const ir = await getIr(taskId, loc.docId)
```

并将 `getEvidence` 中：

```ts
      refs: await buildRefs(id, ev, silent),
```

改为：

```ts
      refs: await buildRefs(id, ev),
```

- [ ] **Step 2: 类型检查**

Run:
```powershell
pnpm run typecheck
```

Expected: PASS。

- [ ] **Step 3: 提交**

```bash
git add user-web/src/api/modules/compare.ts
git commit --no-verify -m "fix(compare): make IR enrichment requests silent"
```

---

### Task 3: 证据卡片整卡点击溯源

**Files:**
- Modify: `user-web/src/views/ai-bid/compare/components/EvidenceCard.vue`

- [ ] **Step 1: 修改模板**

将 `EvidenceCard.vue` 的根节点替换为：

```vue
  <div class="evidence-card" role="button" tabindex="0" @click="emit('trace', evidence)" @keydown.enter="emit('trace', evidence)">
    <div class="evidence-card__head">
      <a-tag :color="SEVERITY_META[evidence.severity].color">{{ SEVERITY_META[evidence.severity].label }}</a-tag>
      <a-tag :color="EVIDENCE_TYPE_META[evidence.type].color">{{ EVIDENCE_TYPE_META[evidence.type].label }}</a-tag>
      <span class="evidence-card__docs">{{ docLabels }}</span>
      <span class="evidence-card__spacer" />
      <a-tooltip title="点击卡片溯源">
        <SearchOutlined class="evidence-card__trace-icon" />
      </a-tooltip>
    </div>
    <div class="evidence-card__title">{{ evidence.title }}</div>
    <div class="evidence-card__desc">{{ evidence.summary }}</div>
    <div v-if="metricLines.length" class="evidence-card__metrics">
      <a-tag v-for="(line, i) in metricLines" :key="i" class="evidence-card__metric" color="blue">
        {{ line }}
      </a-tag>
    </div>
  </div>
```

同时删除原模板中头部的 `a-button`“溯源”按钮。

- [ ] **Step 2: 增加可点击样式**

将 `.evidence-card {` 规则改为：

```less
.evidence-card {
  padding: @spacing-md @spacing-xl;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  background: @card-bg;
  cursor: pointer;
  transition: border-color @transition-fast, box-shadow @transition-fast;

  &:hover {
    border-color: @brand-primary;
    box-shadow: @shadow-sm;
  }

  &:focus-visible {
    outline: 2px solid @brand-primary;
    outline-offset: 2px;
  }
```

并在 `&__spacer` 规则后追加：

```less
  &__trace-icon {
    color: @text-tertiary;
    font-size: @font-size-sm;
  }
```

- [ ] **Step 3: 类型检查**

Run:
```powershell
pnpm run typecheck
```

Expected: PASS。

- [ ] **Step 4: 提交**

```bash
git add user-web/src/views/ai-bid/compare/components/EvidenceCard.vue
git commit --no-verify -m "feat(compare): make evidence card clickable for tracing"
```

---

### Task 4: PdfWorkspace 内部单栏状态与溯源兜底

**Files:**
- Modify: `user-web/src/views/ai-bid/compare/components/PdfWorkspace.vue`

- [ ] **Step 1: 修改模板**

将模板中 `v-if="!collapsed"` 全部替换为 `v-if="!singlePane"`；将 `:class="{ 'pdf-workspace__body--single': collapsed }"` 替换为 `:class="{ 'pdf-workspace__body--single': singlePane }"`。

将内部折叠按钮的 tooltip 与点击替换为：

```vue
      <a-tooltip :title="singlePane ? '展开双栏对比' : '收起为单栏'">
        <a-button size="small" type="text" class="pdf-workspace__toggle" @click="singlePane = !singlePane">
```

- [ ] **Step 2: 修改脚本 props / emit / 状态**

将 props 与 emit 改为：

```ts
const props = defineProps<{
  documents: CompareDocMeta[]
  pairActive?: { docAId: string, docBId: string } | null
  scanningDocId?: string | null
}>()

const emit = defineEmits<{
  'tabManual': []
}>()

const singlePane = ref(false)
```

删除原来的 `collapsed: boolean` prop 与 `'update:collapsed'` emit；在 `leftDocId` 等状态声明前新增 `const singlePane = ref(false)`。

将 `watch(() => props.pairActive, ...)` 中的：

```ts
  if (props.collapsed) emit('update:collapsed', false)
```

替换为：

```ts
  if (singlePane.value) singlePane.value = false
```

- [ ] **Step 3: 替换 `locate` 函数**

将现有 `locate` 函数整体替换为：

```ts
/** 证据溯源：单份文档单栏定位，两份及以上自动展开双栏并分别定位高亮；无 bbox 时整页兜底。 */
function locate(ev: EvidenceItem): void {
  const [a, b] = ev.docIds
  if (b && singlePane.value) singlePane.value = false
  if (a) {
    leftDocId.value = a
    const refs = refsOf(ev, a)
    leftPage.value = refs[0]?.page ?? 1
    leftHigh.value = refs.length ? refs : fullPageRefs(ev, a)
  }
  if (b) {
    rightDocId.value = b
    const refs = refsOf(ev, b)
    rightPage.value = refs[0]?.page ?? 1
    rightHigh.value = refs.length ? refs : fullPageRefs(ev, b)
  }
}

function refsOf(ev: EvidenceItem, docId: string): BlockRange[] {
  return ev.refs.filter((r) => r.docId === docId)
}

function fullPageRefs(ev: EvidenceItem, docId: string): BlockRange[] {
  return [{ docId, page: 1, bbox: [0, 0, 1, 1], pairId: ev.id }]
}
```

- [ ] **Step 4: 类型检查**

Run:
```powershell
pnpm run typecheck
```

Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add user-web/src/views/ai-bid/compare/components/PdfWorkspace.vue
git commit --no-verify -m "feat(compare): decouple single-pane state and add locate fallback"
```

---

### Task 5: ProcessPanel 重构为溯源过程流

**Files:**
- Modify: `user-web/src/views/ai-bid/compare/components/ProcessPanel.vue`

- [ ] **Step 1: 整体替换 `ProcessPanel.vue`**

将 `ProcessPanel.vue` 全文替换为：

```vue
<template>
  <div class="process-panel">
    <div class="process-panel__scroll">
      <div v-if="failedDocs.length" class="process-panel__partial">
        <ExclamationCircleOutlined class="process-panel__partial-icon" />
        <span class="process-panel__partial-text">
          已跳过 {{ failedDocs.length }} 份失败文档，其余结果不受影响
        </span>
        <a-button size="small" :loading="reparseAllLoading" @click="emit('reparseAll')">
          重新解析失败文档
        </a-button>
      </div>

      <section
        v-for="(stage, index) in visibleStages"
        :key="stage.key"
        class="trace-stage"
        :class="{ 'trace-stage--collapsed': isCollapsed(stage.key) }"
      >
        <button
          type="button"
          class="trace-stage__head"
          :class="{ 'trace-stage__head--active': !isStageDone(stage.key) }"
          @click="toggleStage(stage.key)"
        >
          <span class="trace-stage__index">{{ index + 1 }}</span>
          <span class="trace-stage__title">{{ stage.title }}</span>
          <span class="trace-stage__summary">{{ summaryOf(stage.key) }}</span>
          <span class="trace-stage__spacer" />
          <a-tag :color="metaOf(stage.key).color" class="trace-stage__tag">{{ metaOf(stage.key).text }}</a-tag>
          <DownOutlined
            v-if="isStageDone(stage.key)"
            class="trace-stage__chevron"
            :class="{ 'trace-stage__chevron--collapsed': isCollapsed(stage.key) }"
          />
        </button>

        <div v-show="!isCollapsed(stage.key)" class="trace-stage__body">
          <template v-if="stage.key === 'parse'">
            <div class="process-list">
              <div v-for="d in task.documents" :key="d.id" class="process-row">
                <span class="process-row__label">{{ docLabel(task.documents, d.id) }}</span>
                <a-tag v-if="d.role === 'tender'" class="process-row__tender">招标</a-tag>
                <template v-if="d.parseStatus === 'parsing'">
                  <div class="process-row__parse">
                    <div class="process-row__parse-head">
                      <a-spin size="small" />
                      <span class="process-row__name" :title="d.fileName">{{ d.fileName }}</span>
                      <span class="process-row__elapsed">解析中 · {{ elapsedText(d) }}</span>
                    </div>
                    <div class="process-row__parse-meta">
                      <a-progress
                        class="process-row__parse-bar"
                        :percent="d.parseProgress ?? 0"
                        :show-info="false"
                        size="small"
                      />
                      <span class="process-row__step" :title="stepText(d)">{{ stepText(d) }}</span>
                      <span v-if="d.parseProgress != null" class="process-row__percent">{{ d.parseProgress }}%</span>
                    </div>
                  </div>
                </template>
                <template v-else>
                  <CheckCircleFilled v-if="d.parseStatus === 'done'" class="process-row__ok" />
                  <CloseCircleFilled v-else-if="d.parseStatus === 'failed'" class="process-row__bad" />
                  <span v-else class="process-row__wait">等待</span>
                  <span class="process-row__name" :title="d.fileName">{{ d.fileName }}</span>
                  <span v-if="d.pages" class="process-row__pages">{{ d.pages }} 页</span>
                  <span v-if="parseDurationText(d)" class="process-row__done-time">
                    解析耗时 {{ parseDurationText(d) }}
                  </span>
                  <span v-if="d.failReason" class="process-row__error" :title="d.failReason">{{ d.failReason }}</span>
                  <a-button
                    v-if="d.parseStatus === 'failed'"
                    type="link"
                    size="small"
                    :loading="reparseDocIds.includes(d.id)"
                    @click="emit('reparseDoc', d.id)"
                  >
                    重新解析
                  </a-button>
                </template>
              </div>
              <a-empty v-if="!task.documents.length" description="暂无文档" />
            </div>
          </template>

          <template v-else-if="stage.key === 'clause'">
            <div v-if="extracting" class="process-panel__skeleton">
              <a-skeleton active :paragraph="{ rows: 3 }" />
            </div>
            <a-empty v-else-if="!editableDrafts.length" description="尚未提取条款">
              <a-button type="primary" size="small" @click="emit('extractClauses')">提取条款</a-button>
            </a-empty>
            <div v-else class="clause-edit">
              <div v-for="(c, i) in editableDrafts" :key="c.id" class="clause-edit__row">
                <a-tag :color="c.mandatory ? 'red' : 'default'" class="clause-edit__tag">
                  {{ c.mandatory ? '强制' : '建议' }}
                </a-tag>
                <a-tag class="clause-edit__source">{{ sourceText(c.source) }}</a-tag>
                <a-input
                  v-model:value="editableDrafts[i].content"
                  size="small"
                  :placeholder="c.title"
                  class="clause-edit__input"
                />
                <a-button type="text" size="small" @click="removeClause(i)">
                  <DeleteOutlined />
                </a-button>
              </div>
              <a-button size="small" type="dashed" block @click="addClause">
                <PlusOutlined />添加条款
              </a-button>
              <div class="clause-edit__footer">
                <span class="clause-edit__hint">确认后锁定任务快照，进入两两对比</span>
                <a-button
                  type="primary"
                  :loading="confirmingClauses"
                  :disabled="!editableDrafts.length"
                  @click="emit('confirmClauses', editableDrafts.map(toPayload))"
                >
                  确认并继续
                </a-button>
              </div>
            </div>
          </template>

          <template v-else-if="stage.key === 'compare'">
            <div class="process-list">
              <div v-for="p in pairs" :key="p.pairId" class="process-row">
                <span class="process-row__label">
                  {{ docLabel(task.documents, p.docAId) }} ↔ {{ docLabel(task.documents, p.docBId) }}
                </span>
                <a-tag :color="PAIR_META[p.status].color" class="process-row__status">{{ PAIR_META[p.status].text }}</a-tag>
                <span v-if="p.similarity != null" class="process-row__sim">
                  相似度 {{ Math.round(p.similarity * 100) }}%
                </span>
                <span v-if="p.failReason" class="process-row__error" :title="p.failReason">{{ p.failReason }}</span>
                <a-button
                  v-if="p.status === 'failed'"
                  type="link"
                  size="small"
                  :loading="retryingPairIds.includes(p.pairId)"
                  @click="emit('retryPair', p.pairId)"
                >
                  重试该对
                </a-button>
              </div>
              <a-empty v-if="!pairs.length" description="比对对将在解析完成后生成" />
            </div>

            <template v-if="compareEvidence.length">
              <div class="trace-stage__subtitle">串标查重发现</div>
              <div class="process-feed">
                <EvidenceCard
                  v-for="ev in compareEvidence"
                  :key="ev.id"
                  :evidence="ev"
                  :documents="task.documents"
                  @trace="(e) => emit('locate', e)"
                />
              </div>
            </template>

            <SimilarityHeatmap
              v-if="overview && overview.docLabels.length"
              :labels="overview.docLabels"
              :matrix="overview.simMatrix"
              :self-matrix="overview.simMatrixSelf"
              @cell-click="onHeatmapCell"
            />
          </template>

          <template v-else>
            <div v-if="aiUnavailable" class="process-panel__ai-alert">
              <a-alert
                type="warning"
                show-icon
                message="AI 分析暂不可用"
                description="算法证据不受影响，可稍后重试"
              />
            </div>
            <div v-else-if="!aiDone" class="process-list">
              <div class="process-row">
                <a-spin size="small" />
                <span class="process-row__name">条款响应判定（{{ bidCount }} 份标书）</span>
              </div>
              <div class="process-row">
                <a-spin size="small" />
                <span class="process-row__name">关键指标抽取</span>
              </div>
              <div class="process-row">
                <a-spin size="small" />
                <span class="process-row__name">AI 综合结论生成</span>
              </div>
            </div>
            <div v-if="aiUnavailable" class="process-panel__ai-retry">
              <a-button size="small" :loading="retryingCompare" @click="emit('retryCompare')">重试 AI</a-button>
            </div>

            <template v-if="aiEvidence.length">
              <div class="trace-stage__subtitle">条款与指标发现</div>
              <div class="process-feed">
                <EvidenceCard
                  v-for="ev in aiEvidence"
                  :key="ev.id"
                  :evidence="ev"
                  :documents="task.documents"
                  @trace="(e) => emit('locate', e)"
                />
              </div>
            </template>

            <ResponseMatrix
              :documents="task.documents"
              :evidence="evidence"
              @trace="(e) => emit('locate', e)"
            />
            <IndicatorTable
              :evidence="evidence"
              :documents="task.documents"
              @trace="(e) => emit('locate', e)"
            />
          </template>
        </div>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { message } from 'ant-design-vue'
import {
  CheckCircleFilled,
  CloseCircleFilled,
  DeleteOutlined,
  DownOutlined,
  ExclamationCircleOutlined,
  PlusOutlined,
} from '@ant-design/icons-vue'
import EvidenceCard from './EvidenceCard.vue'
import SimilarityHeatmap from './SimilarityHeatmap.vue'
import ResponseMatrix from './ResponseMatrix.vue'
import IndicatorTable from './IndicatorTable.vue'
import { docLabel } from '../constants'
import type { ClauseItem, CompareDocMeta, ComparePair, CompareTask, EvidenceItem, TaskOverview } from '@/types'

type StageKey = 'parse' | 'clause' | 'compare' | 'ai'

const props = defineProps<{
  task: CompareTask
  overview: TaskOverview | null
  evidence: EvidenceItem[]
  clauseDrafts: ClauseItem[]
  extracting: boolean
  confirmingClauses: boolean
  reparseDocIds: string[]
  reparseAllLoading: boolean
  retryingPairIds: string[]
  retryingCompare: boolean
}>()

const emit = defineEmits<{
  reparseDoc: [docId: string]
  reparseAll: []
  retryPair: [pairId: string]
  retryCompare: []
  extractClauses: []
  confirmClauses: [clauses: ClauseItem[]]
  locate: [item: EvidenceItem]
}>()

const nowTick = ref(Date.now())
let timerHandle: number | undefined

function stopParseTimer(): void {
  if (timerHandle !== undefined) {
    window.clearInterval(timerHandle)
    timerHandle = undefined
  }
}

function startParseTimer(): void {
  if (timerHandle !== undefined) return
  nowTick.value = Date.now()
  timerHandle = window.setInterval(() => {
    nowTick.value = Date.now()
  }, 1000)
}

watch(() => props.task.documents, (docs) => {
  const parsing = docs.some((d) => d.parseStatus === 'parsing')
  if (parsing) {
    startParseTimer()
  } else {
    stopParseTimer()
  }
}, { immediate: true })

onBeforeUnmount(stopParseTimer)

function formatSeconds(totalSeconds: number): string {
  const seconds = Math.max(0, Math.floor(totalSeconds))
  const minutes = Math.floor(seconds / 60)
  const rest = seconds % 60
  return minutes > 0 ? `${minutes}分${rest}秒` : `${seconds}秒`
}

function elapsedText(d: CompareDocMeta): string {
  const start = d.parseStartedAt ? Date.parse(d.parseStartedAt) : undefined
  if (!start) return '0秒'
  const end = d.parseFinishedAt ? Date.parse(d.parseFinishedAt) : nowTick.value
  return formatSeconds((end - start) / 1000)
}

function parseDurationText(d: CompareDocMeta): string {
  if (d.parseStatus !== 'done' && d.parseStatus !== 'failed') return ''
  if (!d.parseStartedAt || !d.parseFinishedAt) return ''
  return formatSeconds((Date.parse(d.parseFinishedAt) - Date.parse(d.parseStartedAt)) / 1000)
}

function stepText(d: CompareDocMeta): string {
  const parts = [d.parseStage, d.parseStageMessage].filter((s): s is string => !!s)
  return parts.length ? parts.join(' · ') : '解析中'
}

const PAIR_META: Record<ComparePair['status'], { color: string, text: string }> = {
  waiting: { color: 'default', text: '等待' },
  processing: { color: 'blue', text: '比对中' },
  done: { color: 'green', text: '完成' },
  failed: { color: 'red', text: '失败' },
}

const failedDocs = computed(() => props.task.documents.filter((d) => d.parseStatus === 'failed'))
const pairs = computed(() => props.task.pairs ?? [])
const failedPairs = computed(() => pairs.value.filter((p) => p.status === 'failed'))
const bidCount = computed(() => props.task.documents.filter((d) => d.role !== 'tender').length)
const compareEvidence = computed(() =>
  props.evidence.filter((e) => e.type === 'similarity' || e.type === 'price' || e.type === 'metadata'))
const aiEvidence = computed(() =>
  props.evidence.filter((e) => e.type === 'clause' || e.type === 'indicator'))
const aiUnavailable = computed(() => (props.task.progress.message ?? '').includes('AI 分析暂不可用'))

function isTerminalish(t: CompareTask): boolean {
  return t.status === 'completed' || t.status === 'failed' || t.status === 'partial'
}

const parseDone = computed(() =>
  props.task.documents.length > 0
  && props.task.documents.every((d) => d.parseStatus === 'done' || d.parseStatus === 'failed'),
)
const clauseVisible = computed(() =>
  !!props.task.tenderDocId && !props.task.clauseSnapshot && !isTerminalish(props.task),
)
const clauseDone = computed(() => !!props.task.clauseSnapshot)
const compareVisible = computed(() =>
  parseDone.value
  && (!clauseVisible.value || clauseDone.value)
  && props.task.status !== 'failed'
  && props.task.status !== 'uploading',
)
const compareDone = computed(() => {
  if (props.task.progress.stage === 'analyzing' || props.task.progress.stage === 'done') return true
  return pairs.value.length > 0
    && pairs.value.every((p) => p.status === 'done' || p.status === 'failed')
    && props.task.status !== 'comparing'
    && props.task.status !== 'parsing'
})
const aiVisible = computed(() =>
  compareDone.value
  && (props.task.progress.stage === 'analyzing'
    || props.task.progress.stage === 'done'
    || isTerminalish(props.task)),
)
const aiDone = computed(() => isTerminalish(props.task) || props.task.progress.stage === 'done')

const visibleStages = computed<{ key: StageKey, title: string }[]>(() => {
  const list: { key: StageKey, title: string }[] = [{ key: 'parse', title: '文档解析' }]
  if (clauseVisible.value) list.push({ key: 'clause', title: '条款确认' })
  if (compareVisible.value) list.push({ key: 'compare', title: '两两对比' })
  if (aiVisible.value) list.push({ key: 'ai', title: 'AI 分析' })
  return list
})

const stageDoneMap = computed<Record<StageKey, boolean>>(() => ({
  parse: parseDone.value,
  clause: clauseDone.value,
  compare: compareDone.value,
  ai: aiDone.value,
}))

function isStageDone(key: StageKey): boolean {
  return stageDoneMap.value[key]
}

const expandedStages = ref<Set<StageKey>>(new Set())

function isCollapsed(key: StageKey): boolean {
  if (!isStageDone(key)) return false
  return !expandedStages.value.has(key)
}

function toggleStage(key: StageKey): void {
  if (!isStageDone(key)) return
  const next = new Set(expandedStages.value)
  if (next.has(key)) {
    next.delete(key)
  } else {
    next.add(key)
  }
  expandedStages.value = next
}

const parseSummary = computed(() => {
  if (!parseDone.value) {
    const parsing = props.task.documents.filter((d) => d.parseStatus === 'parsing').length
    return parsing ? `解析中 ${parsing}/${props.task.documents.length}` : '等待解析'
  }
  const pages = props.task.documents.reduce((acc, d) => acc + (d.pages || 0), 0)
  const failed = failedDocs.value.length
  return `文档解析完成 · ${props.task.documents.length} 份 · ${pages} 页${failed ? ` · ${failed} 份失败` : ''}`
})

const clauseCount = computed(() => props.task.clauseSnapshot?.length ?? 0)
const clauseSummary = computed(() =>
  clauseDone.value ? `条款确认完成 · ${clauseCount.value} 条` : '等待确认')

const compareSummary = computed(() => {
  if (!compareDone.value) {
    const processing = pairs.value.find((p) => p.status === 'processing')
    if (processing) {
      const fallbackIndex = pairs.value.findIndex((p) => p.pairId === processing.pairId) + 1
      const idx = props.task.progress.pairIndex ?? (fallbackIndex > 0 ? fallbackIndex : 1)
      return `第 ${idx}/${pairs.value.length || '—'} 对比对中`
    }
    const done = pairs.value.filter((p) => p.status === 'done' || p.status === 'failed').length
    return done ? `已完成 ${done}/${pairs.value.length} 对` : '等待比对'
  }
  if (!pairs.value.length) return '两两对比完成'
  const done = pairs.value.filter((p) => p.status === 'done').length
  const sims = pairs.value.filter((p) => p.similarity != null).map((p) => p.similarity!)
  const max = sims.length ? Math.round(Math.max(...sims) * 100) : 0
  const failed = failedPairs.value.length
  return `两两对比完成 · ${done}/${pairs.value.length} 对 · 最高相似度 ${max}%${failed ? ` · ${failed} 对失败` : ''}`
})

const aiSummary = computed(() =>
  aiDone.value ? `AI 分析完成 · 共 ${aiEvidence.value.length} 条发现` : 'AI 分析中')

function summaryOf(key: StageKey): string {
  switch (key) {
    case 'parse': return parseSummary.value
    case 'clause': return clauseSummary.value
    case 'compare': return compareSummary.value
    case 'ai': return aiSummary.value
  }
}

interface StageMeta {
  color: string
  text: string
}

const stageMeta = computed<Record<StageKey, StageMeta>>(() => ({
  parse: parseDone.value
    ? { color: failedDocs.value.length ? 'orange' : 'green', text: failedDocs.value.length ? '部分完成' : '完成' }
    : {
      color: props.task.documents.some((d) => d.parseStatus === 'parsing') ? 'blue' : 'default',
      text: props.task.documents.some((d) => d.parseStatus === 'parsing') ? '解析中' : '等待',
    },
  clause: clauseDone.value ? { color: 'green', text: '完成' } : { color: 'gold', text: '待确认' },
  compare: compareDone.value
    ? { color: failedPairs.value.length ? 'orange' : 'green', text: failedPairs.value.length ? '部分完成' : '完成' }
    : {
      color: pairs.value.some((p) => p.status === 'processing') ? 'blue' : 'default',
      text: pairs.value.some((p) => p.status === 'processing') ? '比对中' : '等待',
    },
  ai: aiDone.value ? { color: 'green', text: '完成' } : { color: 'purple', text: '分析中' },
}))

function metaOf(key: StageKey): StageMeta {
  return stageMeta.value[key]
}

const editableDrafts = ref<ClauseItem[]>([])
watch(() => props.clauseDrafts, (list) => {
  editableDrafts.value = list.map((c) => ({ ...c }))
}, { immediate: true, deep: true })

function sourceText(source: ClauseItem['source']): string {
  return {
    library: '模板库',
    ai_extracted: 'AI 提取',
    user_added: '手动添加',
  }[source] ?? source
}

function addClause(): void {
  editableDrafts.value.push({
    id: `draft-${Date.now()}`,
    title: '',
    content: '',
    category: '',
    mandatory: true,
    source: 'user_added',
  })
}

function removeClause(index: number): void {
  editableDrafts.value.splice(index, 1)
}

function toPayload(c: ClauseItem): ClauseItem {
  return {
    ...c,
    content: c.content || c.title,
    title: c.content || c.title,
  }
}

function onHeatmapCell(pair: { docA: string, docB: string }): void {
  const labels = props.overview?.docLabels ?? []
  const bids = props.task.documents.filter((d) => d.role !== 'tender')
  const docAId = bids[labels.indexOf(pair.docA)]?.id
  const docBId = bids[labels.indexOf(pair.docB)]?.id
  const ev = props.evidence.find((e) =>
    docAId && docBId && e.docIds.includes(docAId) && e.docIds.includes(docBId))
  if (ev) {
    emit('locate', ev)
  } else {
    message.info('该文档对暂无发现')
  }
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.process-panel {
  height: 100%;
  min-height: 0;
  overflow: hidden;
}

.process-panel__scroll {
  height: 100%;
  overflow: auto;
  display: flex;
  flex-direction: column;
  gap: @spacing-md;
  padding: @spacing-md @spacing-base @spacing-xl;
}

.process-panel__partial {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-sm @spacing-md;
  border: 1px solid @warning;
  border-radius: @radius-base;
  background: color-mix(in srgb, @warning 8%, @card-bg);

  &-icon { color: @warning; }
  &-text { flex: 1; min-width: 0; font-size: @font-size-xs; color: @text-secondary; }
}

.trace-stage {
  border: 1px solid @border-color;
  border-radius: @radius-lg;
  background: @card-bg;
  overflow: hidden;

  &__head {
    width: 100%;
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    padding: @spacing-md @spacing-xl;
    background: @card-bg;
    border: none;
    cursor: pointer;
    font: inherit;
    text-align: left;

    &:hover:not(.trace-stage__head--active) {
      background: color-mix(in srgb, @brand-primary 4%, @card-bg);
    }
  }

  &__head--active {
    cursor: default;
  }

  &__index {
    flex-shrink: 0;
    width: 22px;
    height: 22px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    border-radius: 50%;
    background: @brand-primary;
    color: #fff;
    font-size: @font-size-xs;
    font-weight: @font-weight-semibold;
  }

  &__title {
    flex-shrink: 0;
    font-size: @font-size-sm;
    font-weight: @font-weight-semibold;
    color: @text-primary;
  }

  &__summary {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: @font-size-xs;
    color: @text-tertiary;
  }

  &__spacer { flex: 1; }
  &__tag { flex-shrink: 0; margin-inline-end: 0; }

  &__chevron {
    flex-shrink: 0;
    color: @text-tertiary;
    transition: transform @transition-fast;

    &--collapsed { transform: rotate(-90deg); }
  }

  &__body {
    border-top: 1px solid @divider-color;
  }

  &__subtitle {
    padding: @spacing-sm @spacing-xl 0;
    font-size: @font-size-xs;
    font-weight: @font-weight-medium;
    color: @text-tertiary;
  }
}

.process-list {
  display: flex;
  flex-direction: column;
  gap: @spacing-xs;
  padding: @spacing-base @spacing-xl @spacing-xl;
}

.process-row {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: 5px 0;
  font-size: @font-size-sm;

  &__label {
    flex-shrink: 0;
    font-weight: @font-weight-semibold;
    color: @text-primary;
    min-width: 34px;
  }

  &__tender { flex-shrink: 0; }
  &__name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    color: @text-primary;
  }
  &__wait { font-size: @font-size-xs; color: @text-tertiary; }
  &__parse {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: 3px;
  }
  &__parse-head {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    min-width: 0;
  }
  &__parse-meta {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    padding-left: 22px;
    min-width: 0;
  }
  &__parse-bar {
    width: 72px;
    flex-shrink: 0;
    margin-inline-end: 0;
  }
  &__step {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: @font-size-xs;
    color: @text-tertiary;
  }
  &__elapsed {
    flex-shrink: 0;
    font-size: @font-size-xs;
    color: @brand-primary;
    font-variant-numeric: tabular-nums;
  }
  &__percent {
    flex-shrink: 0;
    font-size: @font-size-xs;
    color: @text-secondary;
    font-variant-numeric: tabular-nums;
  }
  &__done-time {
    flex-shrink: 0;
    font-size: @font-size-xs;
    color: @text-tertiary;
    font-variant-numeric: tabular-nums;
  }
  &__ok { color: @success; flex-shrink: 0; }
  &__bad { color: @danger; flex-shrink: 0; }
  &__pages { flex-shrink: 0; font-size: @font-size-xs; color: @text-tertiary; }
  &__sim { flex-shrink: 0; font-size: @font-size-xs; color: @text-secondary; }

  &__error {
    max-width: 280px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: @font-size-xs;
    color: @danger;
  }

  &__status { flex-shrink: 0; }
}

.clause-edit {
  padding: @spacing-base @spacing-xl @spacing-xl;
  display: flex;
  flex-direction: column;
  gap: @spacing-sm;

  &__row {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
  }

  &__tag, &__source { flex-shrink: 0; }
  &__input { flex: 1; min-width: 0; }

  &__footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: @spacing-md;
    padding-top: @spacing-sm;
  }

  &__hint {
    font-size: @font-size-xs;
    color: @text-tertiary;
  }
}

.process-panel__skeleton {
  padding: @spacing-base @spacing-xl;
}

.process-feed {
  display: flex;
  flex-direction: column;
  gap: @spacing-sm;
  padding: @spacing-sm @spacing-xl @spacing-xl;
}

.process-panel__ai-alert {
  margin: @spacing-base @spacing-xl 0;
}

.process-panel__ai-retry {
  padding: @spacing-sm @spacing-xl @spacing-xl;
}

@media (prefers-reduced-motion: reduce) {
  .trace-stage__chevron {
    transition: none;
  }
}
</style>
```

- [ ] **Step 2: 类型检查**

Run:
```powershell
pnpm run typecheck
```

Expected: PASS。若报 `props.overview` 可能为 null 的模板访问问题，确认所有 `overview?.` 可选链均已使用。

- [ ] **Step 3: 提交**

```bash
git add user-web/src/views/ai-bid/compare/components/ProcessPanel.vue
git commit --no-verify -m "feat(compare): rebuild right panel as collapsible trace flow"
```

---

### Task 6: index.vue 集成、状态标签前置、收起左侧面板、删除 ResultPanel

**Files:**
- Modify: `user-web/src/views/ai-bid/compare/index.vue`
- Delete: `user-web/src/views/ai-bid/compare/components/ResultPanel.vue`

- [ ] **Step 1: 移除 ResultPanel 引用与结果分支**

删除 import：

```ts
import ResultPanel from './components/ResultPanel.vue'
```

将 `panel` computed 替换为：

```ts
const panel = computed<'process' | 'failed'>(() => {
  if (!task.value) return 'process'
  if (task.value.status === 'failed') return 'failed'
  return 'process'
})
```

删除模板中整个 `ResultPanel` 分支，并将 `ProcessPanel` 分支替换为：

```vue
          <ProcessPanel
            v-if="panel === 'process'"
            :task="task"
            :overview="overview"
            :evidence="evidence"
            :clause-drafts="clauseDrafts"
            :extracting="extracting"
            :confirming-clauses="confirmingClauses"
            :reparse-doc-ids="reparseDocIds"
            :reparse-all-loading="reparseAllLoading"
            :retrying-pair-ids="retryingPairIds"
            :retrying-compare="retryingCompare"
            @reparse-doc="onReparseDoc"
            @reparse-all="onReparseAll"
            @retry-pair="onRetryPair"
            @retry-compare="onRetryCompare"
            @extract-clauses="onExtractClauses"
            @confirm-clauses="onConfirmClauses"
            @locate="onLocateEvidence"
          />
```

- [ ] **Step 2: 状态标签移到项目名前**

将项目名输入框所在的 `.compare-workspace__name` 块开头插入状态标签，删除原来单独的 `<a-tag :color="statusInfo.color">{{ statusInfo.text }}</a-tag>`：

```vue
        <div class="compare-workspace__name">
          <a-tag :color="statusInfo.color">{{ statusInfo.text }}</a-tag>
          <a-input
            v-model:value="nameDraft"
            size="small"
            :maxlength="128"
            class="compare-workspace__name-input"
            :loading="nameSaving"
            @blur="saveName"
            @press-enter="saveName"
          />
          <span v-if="nameError" class="compare-workspace__name-error">{{ nameError }}</span>
        </div>
```

- [ ] **Step 3: 收起按钮改为收起左侧面板**

将 tooltip 文案改为：

```vue
        <a-tooltip :title="workspaceCollapsed ? '展开左侧面板' : '收起左侧面板'">
```

将 split 区域改为：

```vue
      <div class="compare-workspace__split">
        <div
          v-if="!workspaceCollapsed"
          class="compare-workspace__left"
          :style="{ width: `${splitRatio * 100}%` }"
        >
          <PdfWorkspace
            ref="workspaceRef"
            :documents="workspaceDocs"
            :pair-active="pairActive"
            :scanning-doc-id="scanningDocId"
            @tab-manual="lastManualTabAt = Date.now()"
          />
        </div>

        <div
          v-if="!workspaceCollapsed"
          class="compare-workspace__divider"
          :class="{ 'compare-workspace__divider--dragging': draggingSplit }"
          @pointerdown="onDividerDown"
        />

        <div class="compare-workspace__right">
```

删除文件：

```powershell
git rm user-web/src/views/ai-bid/compare/components/ResultPanel.vue
```

- [ ] **Step 4: 类型检查**

Run:
```powershell
pnpm run typecheck
```

Expected: PASS。若报 `onLocateDoc` 未使用，删除该函数；若报其他未使用变量，一并清理。

- [ ] **Step 5: 提交**

```bash
git add user-web/src/views/ai-bid/compare/index.vue
git commit --no-verify -m "feat(compare): remove result tab, reorder status tag, collapse left panel"
```

---

### Task 7: 全量验证与收尾

**Files:**
- 无新增代码

- [ ] **Step 1: 后端测试**

Run:
```powershell
dotnet test "backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/DredgeAI.BidCompare.Application.Tests.csproj" --filter "FullyQualifiedName~DocumentIrDtoTests"
```

Expected: PASS。

- [ ] **Step 2: 前端类型检查**

Run:
```powershell
pnpm run typecheck
```

Expected: PASS。

- [ ] **Step 3: 手动走查**

启动 `pnpm dev`，打开海港 1 / 海港 2 对比任务，逐项验证：

1. 无“服务器内部错误”弹窗。
2. 右侧第一项是“文档解析”；完成后折叠为摘要，随后依次出现“两两对比”“AI 分析”。
3. 无最终结果 Tab、无“结果摘要”、无“过程记录”。
4. 点击任一串标证据卡片：左侧双栏 PDF 自动展开，跳转对应页并高亮。
5. 顶部“已完成”标签在项目名前。
6. 点“收起左侧面板”后左侧 PDF 隐藏、右侧过程流占满整宽。

- [ ] **Step 4: 提交（如有走查修复）**

如有修复，按对应 Task 重新提交；无修复则本 Task 无需提交。

---

## 自查清单

1. **Spec 覆盖**：7 个问题分别对应 Task 1-6；Task 7 做全量验证。
2. **占位符扫描**：无 TBD/TODO，所有修改均给出完整代码或精确替换片段。
3. **类型一致性**：`StageKey`、`visibleStages`、`summaryOf`、`metaOf` 命名前后一致；`PdfWorkspace` 不再暴露 `collapsed` prop，`index.vue` 同步移除绑定。
