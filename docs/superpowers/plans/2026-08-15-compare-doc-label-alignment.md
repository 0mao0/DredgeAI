# 比标文档别名对齐文件名 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 比标模块的标书别名改为优先对齐文件名中的自报字母（C.docx → C），重复字母并列编号（C、C1），无字母文件按上传顺序补位并跳过已占用字母；同时把上传上限从 5 份提升到 8 份（A–H）。

**Architecture:** 新增共享纯函数 `buildDocLabels / docLabel / overviewDocLabels`（位于 `packages/shared/src/core/utils/compare.ts`），所有前端展示位置（双栏、面板、证据、矩阵、热力图）统一引用；后端只改上传数量常量与对应测试。

**Tech Stack:** TypeScript（Node 24 原生跑 TS 单测）、Vue 3 `<script setup>`、LESS、C# / ABP（.NET 8）、xUnit + Shouldly。

---

## 环境注意事项（先读）

- 本仓库大量 `.vue` / `.cs` 文件是 **CRLF 行尾**。`apply_patch` 在这台机器上匹配不了 CRLF 文件；若 patch 报 `Failed to find expected lines`，先对该文件做临时转换：UTF-8 无 BOM 读取 → 把 `\r\n` 替换成 `\n` 写回 → 改完再把 `\n` 替换回 `\r\n`。转换属于机械重写，允许用 PowerShell 完成。
- Node 版本必须 ≥ 23.6（本机为 24.3），`node --test xxx.ts` 可直接跑 TypeScript。
- 系统 `dotnet` 命令是空壳，后端命令统一用：
  `$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'`
- 工作区已有大量未提交的用户改动，**只改本计划列出的文件**，不碰无关文件，不要 `git add -A`。

---

### Task 1: 共享别名工具（TDD：先写失败测试）

**Files:**
- Create: `user-web/__tests__/compare-labels.test.ts`
- Create: `packages/shared/src/core/utils/compare.ts`
- Modify: `packages/shared/src/core/utils/index.ts`
- Modify: `user-web/src/views/ai-bid/compare/constants.ts`（删除本地 `docLabel`，改为 re-export）

- [ ] **Step 1: 写失败测试**

创建 `user-web/__tests__/compare-labels.test.ts`：

```ts
import { test } from 'node:test'
import assert from 'node:assert/strict'
import {
  MAX_BID_DOCUMENTS,
  buildDocLabels,
  docLabel,
  overviewDocLabels,
} from '../../packages/shared/src/core/utils/compare.ts'

const doc = (id: string, fileName: string, role?: string) => ({ id, fileName, role })

test('文件名自带字母时别名对齐（C.docx / D.docx）', () => {
  const docs = [doc('d1', 'C.docx'), doc('d2', 'D.docx')]
  assert.deepEqual(buildDocLabels(docs), { d1: 'C', d2: 'D' })
})

test('中文前缀与括号中的字母也能识别', () => {
  assert.equal(docLabel([doc('d1', '标书C.docx')], 'd1'), 'C')
  assert.equal(docLabel([doc('d1', '技术标（C）.pdf')], 'd1'), 'C')
})

test('文件名含多个独立字母时取第一个', () => {
  assert.equal(docLabel([doc('d1', '标书C-D.docx')], 'd1'), 'C')
})

test('嵌入英文单词/数字串的字母不识别，按顺序补位', () => {
  const docs = [doc('d1', 'CIF条款.pdf'), doc('d2', 'B2B报价.docx')]
  assert.deepEqual(buildDocLabels(docs), { d1: 'A', d2: 'B' })
})

test('A-H 之外的字母不参与自报', () => {
  assert.equal(docLabel([doc('d1', 'Z.docx')], 'd1'), 'A')
})

test('重复字母并列编号 C / C1', () => {
  const docs = [doc('d1', 'C.docx'), doc('d2', '标书C.docx')]
  assert.deepEqual(buildDocLabels(docs), { d1: 'C', d2: 'C1' })
})

test('无字母文件从 A 补位并跳过已占用字母', () => {
  const docs = [doc('d1', 'C.docx'), doc('d2', 'D.docx'), doc('d3', '无字母.docx'), doc('d4', '报价.pdf')]
  assert.deepEqual(buildDocLabels(docs), { d1: 'C', d2: 'D', d3: 'A', d4: 'B' })

  const three = [doc('d1', 'C.docx'), doc('d2', 'D.docx'), doc('d3', '无字母1.pdf'), doc('d4', '无字母2.pdf'), doc('d5', '无字母3.pdf')]
  assert.deepEqual(buildDocLabels(three), { d1: 'C', d2: 'D', d3: 'A', d4: 'B', d5: 'E' })
})

test('招标文件固定显示「招标」', () => {
  const docs = [doc('d1', 'C.docx'), doc('t1', '招标文件.pdf', 'tender')]
  assert.equal(docLabel(docs, 't1'), '招标')
  assert.equal(docLabel(docs, 'd1'), 'C')
})

test('小写字母统一转大写', () => {
  assert.equal(docLabel([doc('d1', 'c.docx')], 'd1'), 'C')
})

test('overviewDocLabels 按文档生成热力图标签', () => {
  const docs = [doc('d1', 'C.docx'), doc('d2', '无字母.pdf')]
  assert.deepEqual(overviewDocLabels(['d1', 'd2'], docs), ['C', 'A'])
})

test('标书上限为 8 份', () => {
  assert.equal(MAX_BID_DOCUMENTS, 8)
})
```

- [ ] **Step 2: 运行测试，确认失败**

Run: `node --test user-web/__tests__/compare-labels.test.ts`
Expected: FAIL，报 `ERR_MODULE_NOT_FOUND` / 找不到 `compare.ts`（模块不存在）。

- [ ] **Step 3: 创建共享工具并接线**

创建 `packages/shared/src/core/utils/compare.ts`：

```ts
export const MAX_BID_DOCUMENTS = 8

const BID_LETTERS = 'ABCDEFGH'

export interface DocLabelSource {
  id: string
  role?: string
  fileName: string
}

const SELF_LETTER_RE = /(?:^|[^A-Z0-9])([A-H])(?=$|[^A-Z0-9])/i

function extractSelfLetter(fileName: string): string | null {
  const stem = fileName.replace(/\.[^.]+$/, '').trim()
  const match = stem.match(SELF_LETTER_RE)
  return match ? match[1].toUpperCase() : null
}

export function buildDocLabels(documents: DocLabelSource[]): Record<string, string> {
  const labels: Record<string, string> = {}
  const claimed = new Set<string>()
  const claimCounts = new Map<string, number>()
  const unclaimed: DocLabelSource[] = []

  for (const doc of documents) {
    if (doc.role === 'tender') {
      labels[doc.id] = '招标'
      continue
    }
    const letter = extractSelfLetter(doc.fileName)
    if (letter) {
      const count = (claimCounts.get(letter) ?? 0) + 1
      claimCounts.set(letter, count)
      claimed.add(letter)
      labels[doc.id] = count === 1 ? letter : `${letter}${count - 1}`
    } else {
      unclaimed.push(doc)
    }
  }

  let next = 0
  for (const doc of unclaimed) {
    while (next < BID_LETTERS.length && claimed.has(BID_LETTERS[next])) next++
    labels[doc.id] = next < BID_LETTERS.length ? BID_LETTERS[next] : doc.id
    next++
  }
  return labels
}

export function docLabel(documents: DocLabelSource[], docId: string): string {
  return buildDocLabels(documents)[docId] ?? docId
}

export function overviewDocLabels(docIds: string[], documents: DocLabelSource[]): string[] {
  return docIds.map((docId) => docLabel(documents, docId))
}
```

修改 `packages/shared/src/core/utils/index.ts`，追加一行：

```ts
export * from './compare'
```

修改 `user-web/src/views/ai-bid/compare/constants.ts`：删除本地 `docLabel` 定义及其注释，替换为：

```ts
export { buildDocLabels, docLabel, isPdfFileName, MAX_BID_DOCUMENTS, overviewDocLabels } from '@shared/core/utils/compare'
```

- [ ] **Step 4: 运行测试，确认通过**

Run: `node --test user-web/__tests__/compare-labels.test.ts`
Expected: 11 个测试全部 PASS。

- [ ] **Step 5: 提交**

```bash
git add user-web/__tests__/compare-labels.test.ts packages/shared/src/core/utils/compare.ts packages/shared/src/core/utils/index.ts user-web/src/views/ai-bid/compare/constants.ts
git commit -m "feat: 比标文档别名对齐文件名（A-H、重复字母并列编号）"
```

---

### Task 2: 各组件统一使用共享 docLabel

**Files:**
- Modify: `user-web/src/views/ai-bid/compare/components/CollusionPanel.vue`
- Modify: `user-web/src/views/ai-bid/compare/components/EvidenceTable.vue`
- Modify: `user-web/src/views/ai-bid/compare/components/MetricsTable.vue`
- Modify: `user-web/src/views/ai-bid/compare/components/ResponseMatrix.vue`
- Modify: `user-web/src/views/ai-bid/compare/components/EvidenceCard.vue`
- Modify: `user-web/src/views/ai-bid/compare/components/IndicatorTable.vue`

- [ ] **Step 1: CollusionPanel.vue**

脚本区删除本地函数（整段删除）：

```ts
function docLabel(docId: string): string {
  const idx = props.documents.findIndex((d) => d.id === docId)
  return idx >= 0 ? String.fromCharCode(65 + idx) : docId
}
```

在 `import SectionCard from ...` 后新增：

```ts
import { docLabel } from '../constants'
```

模板表头改为：

```html
<th v-for="d in documents" :key="d.id" class="meta-table__head">{{ docLabel(documents, d.id) }}</th>
```

- [ ] **Step 2: EvidenceTable.vue**

删除本地 `docLabel`，新增 `import { docLabel } from '../constants'`；模板涉及文档列改为：

```html
{{ (record.docIds as string[]).map((id) => docLabel(documents ?? [], id)).join(' / ') }}
```

- [ ] **Step 3: MetricsTable.vue**

删除本地 `docLabel`、`documents` computed，并整行删除 `import { computed } from 'vue'`（该组件不再使用 computed），新增 `import { docLabel } from '../constants'`；模板表头改为：

```html
<th v-for="d in documents" :key="d.id" class="metrics-table__head">{{ docLabel(documents, d.id) }}</th>
```

- [ ] **Step 4: ResponseMatrix.vue**

删除本地 `docLabel`，新增 `import { docLabel } from '../constants'`；模板表头改为：

```html
<th v-for="d in bidDocs" :key="d.id" class="matrix-table__head">{{ docLabel(bidDocs, d.id) }}</th>
```

- [ ] **Step 5: EvidenceCard.vue**

删除本地 computed 内的内联映射，改为：

```ts
const docLabels = computed(() =>
  props.evidence.docIds.map((id) => docLabel(props.documents ?? [], id)).join(' / '),
)
```

并新增 `import { docLabel } from '../constants'`。

- [ ] **Step 6: IndicatorTable.vue**

columns 的文档列标题改为：

```ts
...props.documents.map((d) => ({
  title: docLabel(props.documents, d.id),
  dataIndex: d.id,
  width: 220,
})),
```

并新增 `import { docLabel } from '../constants'`。

- [ ] **Step 7: 类型检查 + 提交**

Run: `pnpm --filter user-web typecheck`
Expected: 无错误。

```bash
git add user-web/src/views/ai-bid/compare/components/CollusionPanel.vue user-web/src/views/ai-bid/compare/components/EvidenceTable.vue user-web/src/views/ai-bid/compare/components/MetricsTable.vue user-web/src/views/ai-bid/compare/components/ResponseMatrix.vue user-web/src/views/ai-bid/compare/components/EvidenceCard.vue user-web/src/views/ai-bid/compare/components/IndicatorTable.vue
git commit -m "refactor: 比标各组件统一使用共享文档别名"
```

---

### Task 3: 热力图标签走同一套别名

**Files:**
- Modify: `user-web/src/api/modules/compare.ts`（`getOverview`）

- [ ] **Step 1: 修改 getOverview**

把：

```ts
const [matrix, evidence] = await Promise.all([getMatrix(id), getEvidence(id)])
const docLabels = matrix.docIds.map((_, i) => String.fromCharCode(65 + i))
```

改为：

```ts
const [matrix, evidence, documents] = await Promise.all([getMatrix(id), getEvidence(id), getDocuments(id)])
const docLabels = overviewDocLabels(matrix.docIds, documents)
```

并在文件顶部 import 区追加：

```ts
import { overviewDocLabels } from '@shared/core/utils/compare'
```

- [ ] **Step 2: 类型检查 + 提交**

Run: `pnpm --filter user-web typecheck`
Expected: 无错误。

```bash
git add user-web/src/api/modules/compare.ts
git commit -m "feat: 比标热力图标签与页面别名保持一致"
```

---

### Task 4: 上传上限 5 → 8（后端 TDD + 前端）

**Files:**
- Modify: `backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/CompareTasks/CompareTaskAppServiceTests.cs`
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/CompareTasks/CompareTaskAppService.cs`
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/Drafts/CompareDraftAppService.cs`
- Modify: `user-web/src/views/ai-bid/compare/index.vue`
- Modify: `user-web/src/views/ai-bid/compare/components/UploadPage.vue`

- [ ] **Step 1: 改后端测试（先红）**

在 `CompareTaskAppServiceTests.cs` 中把方法 `UploadDocument_Should_Enforce_Max_5_Bid_Documents` 整体替换为：

```csharp
[Fact]
public async Task UploadDocument_Should_Enforce_Max_8_Bid_Documents()
{
    var task = await _appService.CreateAsync(new CreateCompareTaskDto { Name = "t" });
    for (var i = 0; i < 8; i++)
    {
        await _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, $"标书{i}.pdf",
            new MemoryStream(new byte[] { 1 }));
    }

    var ex = await Should.ThrowAsync<BusinessException>(() =>
        _appService.UploadDocumentAsync(task.Id, DocumentRole.Bid, "第9份.pdf",
            new MemoryStream(new byte[] { 1 })));
    ex.Code.ShouldBe(BidCompareErrorCodes.DocumentCountOutOfRange);
}
```

- [ ] **Step 2: 运行该测试，确认失败**

Run:
```powershell
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
& $dotnet test "backend\DredgeAI.BidCompare\test\DredgeAI.BidCompare.Application.Tests\DredgeAI.BidCompare.Application.Tests.csproj" --filter "FullyQualifiedName~UploadDocument_Should_Enforce_Max_8_Bid_Documents" --nologo
```
Expected: FAIL（当前第 6 份就会抛 `DocumentCountOutOfRange`）。

- [ ] **Step 3: 改后端常量**

`CompareTaskAppService.cs` 第 32 行：
```csharp
private const int MaxBidDocuments = 5;
```
改为 `= 8;`。

`CompareDraftAppService.cs` 第 19 行同样改为 `= 8;`。

- [ ] **Step 4: 运行测试，确认通过**

Run: 同 Step 2 命令。
Expected: PASS。

- [ ] **Step 5: 改前端上限**

`user-web/src/views/ai-bid/compare/index.vue`：
- import 追加 `MAX_BID_DOCUMENTS`（从 `'./constants'`）；
- 上传校验改为：

```ts
if (activeBids >= MAX_BID_DOCUMENTS) {
  pushUploadItem(key, file, role, `投标文件最多 ${MAX_BID_DOCUMENTS} 份`)
}
```

`user-web/src/views/ai-bid/compare/components/UploadPage.vue`：
- import 追加 `MAX_BID_DOCUMENTS`（从 `'../constants'`）；
- 计数改为 `{{ bidCount }}/{{ MAX_BID_DOCUMENTS }}`；
- 提示改为 `已选 {{ bidCount }} 份，可继续添加至 {{ MAX_BID_DOCUMENTS }} 份`。

- [ ] **Step 6: 类型检查 + 全量验证 + 提交**

Run:
```powershell
pnpm --filter user-web typecheck
node --test user-web/__tests__/compare-labels.test.ts
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
& $dotnet test "backend\DredgeAI.BidCompare\test\DredgeAI.BidCompare.Application.Tests\DredgeAI.BidCompare.Application.Tests.csproj" --nologo --no-restore
```
Expected: typecheck 无错误、前端单测全 PASS、后端 57 个测试全 PASS（含改名后的 8 份上限用例）。

```bash
git add backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/CompareTasks/CompareTaskAppServiceTests.cs backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/CompareTasks/CompareTaskAppService.cs backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/Drafts/CompareDraftAppService.cs user-web/src/views/ai-bid/compare/index.vue user-web/src/views/ai-bid/compare/components/UploadPage.vue
git commit -m "feat: 比标上传上限提升到 8 份"
```

---

### Task 5: 最终回归

- [ ] **Step 1: 全量测试**

Run:
```powershell
node --test user-web/__tests__/compare-labels.test.ts
pnpm --filter user-web typecheck
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
& $dotnet test "backend\DredgeAI.BidCompare\test\DredgeAI.BidCompare.Domain.Tests\DredgeAI.BidCompare.Domain.Tests.csproj" --nologo --no-restore
& $dotnet test "backend\DredgeAI.BidCompare\test\DredgeAI.BidCompare.EntityFrameworkCore.Tests\DredgeAI.BidCompare.EntityFrameworkCore.Tests.csproj" --nologo --no-restore
```
Expected: 全部 PASS。

- [ ] **Step 2: 人工走查（可选）**

重启后端后，上传 `C.docx` + `D.docx`，确认双栏显示 C / D；再传两份无字母文件，确认显示 A / B；传两份同名 C，确认 C / C1。

- [ ] **Step 3: 收尾提交（如有遗漏）**

```bash
git status --short
```
Expected: 若状态里没有本计划涉及的文件，则跳过提交；如有遗漏，只 `git add` 遗漏文件并 `git commit -m "chore: 比标别名对齐收尾"`。
