# Admin-web 标准规范：批量操作与上传文档 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 admin-web 标准规范页新增批量删除、批量解析和“AI 预读 + 后台上传”的上传文档能力（当前全部走前端 mock，API 形状预留真实后端）。

**Architecture:** 页面迁移为 `views/data/static/standards/` 目录结构，`index.vue` 保持唯一状态持有者；新增上传队列 composable、上传弹窗/任务抽屉/批量解析弹窗三个纯展示组件；API 层新增 preview/upload/batch-delete/batch-parse 四个函数与共享类型，mock 路由对应实现。

**Tech Stack:** Vue 3.5 + `<script setup lang="ts">`、ant-design-vue 4.2.6、axios-mock-adapter、LESS（@shared 变量）、pnpm workspace。

---

## 前置说明

- 仓库当前工作区已有大量未提交改动（比标后端、user-web、standards 相关等）。执行本计划时，**每个 commit 只 stage 本任务列出的文件**，禁止 `git add -A` / `git add .`。
- 仓库 pre-commit 钩子会运行全仓 `pnpm run typecheck` 与 eslint，可能耗时 1~3 分钟甚至更久；若钩子因**与本任务无关的既有改动**失败，不要 `--no-verify` 绕过，停下来向用户报告。
- 本计划所有文件编辑使用 `apply_patch`；移动文件使用 PowerShell `Move-Item`（见 Task 4）。
- 验证命令统一为 `pnpm --filter admin-web run typecheck`（快速）与最终 `pnpm run typecheck`（全仓）。

## 文件结构

| 文件 | 职责 |
| --- | --- |
| `packages/shared/src/core/types/standard.ts` | 新增 `StandardPropertyInput`、`StandardParseBatchResult` |
| `packages/shared/src/core/api/urls.ts` | 新增 create/preview/batch-delete/batch-parse 四个 URL key |
| `admin-web/src/api/modules/standards.ts` | 新增 `previewStandard`、`uploadStandard`、`deleteStandards`、`parseStandards` |
| `admin-web/src/mock/routes/standards.ts` | 新增 preview/create/batch-delete/batch-parse 四个 mock 端点 |
| `admin-web/src/views/data/static/standards/index.vue` | 页面（迁移自 `standards.vue`），唯一状态持有者 |
| `admin-web/src/views/data/static/standards/constants.ts` | 选项数组、年份、`statusColor` 共享 |
| `admin-web/src/views/data/static/standards/types.ts` | `StandardParseBatchItem` 页面内类型 |
| `admin-web/src/views/data/static/standards/composables/useStandardUpload.ts` | 上传任务队列（AI 预读进度、后台上传、重试） |
| `admin-web/src/views/data/static/standards/components/StandardPdfViewer.vue` | 从 `views/data/static/components/` 迁移 |
| `admin-web/src/views/data/static/standards/components/StandardMetadataForm.vue` | 元数据表单（纯 props/emits） |
| `admin-web/src/views/data/static/standards/components/StandardUploadModal.vue` | 上传弹窗：多选 + 折叠条 + 预填表单 |
| `admin-web/src/views/data/static/standards/components/StandardUploadTasksDrawer.vue` | 后台任务抽屉 |
| `admin-web/src/views/data/static/standards/components/StandardBatchParseModal.vue` | 批量解析进度弹窗 |
| `admin-web/src/router/manifests.ts` | 页面导入路径改为 `standards/index.vue` |

---

### Task 0: 前置检查

**Files:**
- 无（只读检查）

- [ ] **Step 1: 查看工作区状态**

运行：`git status --short`

Expected：存在大量既有未提交改动；记录当前状态，后续每个 commit 只 add 本任务文件。

- [ ] **Step 2: 运行基线 typecheck**

运行：`pnpm --filter admin-web run typecheck`

Expected：exit 0。若因既有改动失败，先记录错误清单；若错误涉及 `admin-web/src/views/data/static/standards*` 或 `packages/shared/src/core/types/standard.ts`，先修复再做 Task 1；否则继续并留意 commit 时钩子可能失败。

---

### Task 1: 共享类型与 URL

**Files:**
- Modify: `packages/shared/src/core/types/standard.ts`
- Modify: `packages/shared/src/core/api/urls.ts`

- [ ] **Step 1: 在 `standard.ts` 末尾追加两个类型**

使用 `apply_patch` 在 `StandardAIAnalysis` 接口之后追加：

```ts
export interface StandardPropertyInput {
  name: string
  code: string
  industry?: string
  nature?: string
  level?: string
  status?: string
  issuer?: string
  publishYear?: number
  parentId?: string
  description?: string
}

export interface StandardParseBatchResult {
  id: string
  success: boolean
  analysis?: StandardAIAnalysis
  error?: string
}
```

- [ ] **Step 2: 在 `urls.ts` 追加 URL key**

在 `adminStandards` / `adminStandardParse` 附近追加：

```ts
  adminStandardCreate: '/standards',
  adminStandardPreview: '/standards/preview',
  adminStandardsBatchDelete: '/standards/batch-delete',
  adminStandardsBatchParse: '/standards/batch-parse',
```

- [ ] **Step 3: 验证**

运行：`pnpm --filter admin-web run typecheck`

Expected：exit 0。

- [ ] **Step 4: Commit**

```bash
git add packages/shared/src/core/types/standard.ts packages/shared/src/core/api/urls.ts
git commit -m "feat(standards): 增加批量操作与上传的共享类型和 URL"
```

---

### Task 2: API 模块

**Files:**
- Modify: `admin-web/src/api/modules/standards.ts`

- [ ] **Step 1: 用完整文件内容替换 `standards.ts`**

```ts
import request from '@/api/request'
import { urls } from '@shared/core/api'
import type {
  StandardAIAnalysis,
  StandardParseBatchResult,
  StandardProperty,
  StandardPropertyInput,
} from '@/types'
import type { PagedResult } from '@shared/types'

export interface StandardQueryParams {
  keyword?: string
  industry?: string
  nature?: string
  level?: string
  status?: string
  publishYear?: number
  skipCount?: number
  maxResultCount?: number
}

function buildUrl(tpl: string, id: string): string {
  return tpl.replace(':id', id)
}

export function getStandards(params?: StandardQueryParams): Promise<PagedResult<StandardProperty>> {
  return request.get<PagedResult<StandardProperty>>(urls.adminStandards, { params })
}

export function deleteStandard(id: string): Promise<void> {
  return request.delete(buildUrl(urls.adminStandardDelete, id))
}

export function updateStandard(id: string, data: Partial<StandardProperty>): Promise<StandardProperty> {
  return request.put<StandardProperty>(buildUrl(urls.adminStandardUpdate, id), data)
}

export function parseStandard(id: string): Promise<StandardAIAnalysis> {
  return request.post<StandardAIAnalysis>(buildUrl(urls.adminStandardParse, id))
}

/** AI 预读单文件：返回预填的元数据（当前为 mock，真实后端调用 LLM 提取） */
export function previewStandard(file: File): Promise<StandardPropertyInput> {
  const formData = new FormData()
  formData.append('file', file)
  return request.post<StandardPropertyInput>(urls.adminStandardPreview, formData, {
    timeout: 60000,
  })
}

/** 上传 PDF + 元数据，创建标准记录 */
export function uploadStandard(file: File, data: StandardPropertyInput): Promise<StandardProperty> {
  const formData = new FormData()
  formData.append('file', file)
  formData.append('metadata', JSON.stringify(data))
  return request.post<StandardProperty>(urls.adminStandardCreate, formData, {
    timeout: 120000,
  })
}

/** 批量删除，返回成功删除数量 */
export function deleteStandards(ids: string[]): Promise<number> {
  return request.post<number>(urls.adminStandardsBatchDelete, { ids })
}

/**
 * 批量解析。当前实现按 id 串行调用单条解析，逐条回调便于 UI 展示进度；
 * 真实后端提供批量端点后，可整体替换为一次 `POST /standards/batch-parse`。
 */
export async function parseStandards(
  ids: string[],
  onItem?: (result: StandardParseBatchResult) => void,
): Promise<StandardParseBatchResult[]> {
  const results: StandardParseBatchResult[] = []
  for (const id of ids) {
    try {
      const analysis = await parseStandard(id)
      const result: StandardParseBatchResult = { id, success: true, analysis }
      results.push(result)
      onItem?.(result)
    } catch (error) {
      const result: StandardParseBatchResult = {
        id,
        success: false,
        error: error instanceof Error ? error.message : '解析失败，请稍后重试',
      }
      results.push(result)
      onItem?.(result)
    }
  }
  return results
}

/** 标准原文 PDF 静态资源地址（dev/build 均由 Vite public 目录直接提供） */
export function getStandardFileUrl(id: string): string {
  return `/mock/standards/${id}.pdf`
}
```

- [ ] **Step 2: 验证**

运行：`pnpm --filter admin-web run typecheck`

Expected：exit 0。

- [ ] **Step 3: Commit**

```bash
git add admin-web/src/api/modules/standards.ts
git commit -m "feat(standards): 增加预读、上传、批量删除与批量解析 API"
```

---

### Task 3: Mock 路由

**Files:**
- Modify: `admin-web/src/mock/routes/standards.ts`

- [ ] **Step 1: 用完整文件内容替换 `standards.ts`**

> 说明：`POST /standards/batch-parse` mock 端点为后续真实后端批量解析预留；当前 Task 2 的 `parseStandards` 仍按单条解析串行执行以便逐条展示进度，接入真实批量端点后再整体切换。

```ts
import type MockAdapter from 'axios-mock-adapter'
import { standardAIAnalyses, standardProperties } from '@shared/mock/data/standard'
import type {
  StandardAIAnalysis,
  StandardHighlight,
  StandardParseBatchResult,
  StandardProperty,
  StandardPropertyInput,
} from '@/types'

function normalizeNature(nature?: string): string {
  if (nature === '强制性标准') return '强制'
  if (nature === '推荐性标准') return '推荐'
  return nature || '推荐'
}

// 管理端标准规范列表：复用共享标准数据并统一性质文案
const adminStandards: StandardProperty[] = standardProperties.map((p) => ({
  ...p,
  nature: normalizeNature(p.nature),
}))

const PAGE_W = 595
const PAGE_H = 842

// 演示数据：已解析的标准附带原文 bbox 高亮（坐标与 gen-standard-sample-pdf.mjs 的排版一致）
const demoHighlights: Record<string, StandardHighlight[]> = {
  'std-1': [
    { id: 'std-1-title', itemId: 'std-1', page: 1, left: 60 / PAGE_W, top: 72 / PAGE_H, width: 216 / PAGE_W, height: 18 / PAGE_H },
    { id: 'std-1-a1', itemId: 'std-1', page: 2, left: 60 / PAGE_W, top: 120 / PAGE_H, width: 480 / PAGE_W, height: 12 / PAGE_H },
    { id: 'std-1-a1-2', itemId: 'std-1', page: 2, left: 60 / PAGE_W, top: 142 / PAGE_H, width: 132 / PAGE_W, height: 12 / PAGE_H },
    { id: 'std-1-a2', itemId: 'std-1', page: 2, left: 60 / PAGE_W, top: 164 / PAGE_H, width: 480 / PAGE_W, height: 12 / PAGE_H },
    { id: 'std-1-a2-2', itemId: 'std-1', page: 2, left: 60 / PAGE_W, top: 186 / PAGE_H, width: 72 / PAGE_W, height: 12 / PAGE_H },
    { id: 'std-1-a3', itemId: 'std-1', page: 2, left: 60 / PAGE_W, top: 208 / PAGE_H, width: 480 / PAGE_W, height: 12 / PAGE_H },
    { id: 'std-1-a3-2', itemId: 'std-1', page: 2, left: 60 / PAGE_W, top: 230 / PAGE_H, width: 396 / PAGE_W, height: 12 / PAGE_H },
  ],
  'std-2': [
    { id: 'std-2-title', itemId: 'std-2', page: 1, left: 60 / PAGE_W, top: 72 / PAGE_H, width: 198 / PAGE_W, height: 18 / PAGE_H },
    { id: 'std-2-a1', itemId: 'std-2', page: 2, left: 60 / PAGE_W, top: 120 / PAGE_H, width: 480 / PAGE_W, height: 12 / PAGE_H },
    { id: 'std-2-a1-2', itemId: 'std-2', page: 2, left: 60 / PAGE_W, top: 142 / PAGE_H, width: 120 / PAGE_W, height: 12 / PAGE_H },
    { id: 'std-2-a2', itemId: 'std-2', page: 2, left: 60 / PAGE_W, top: 164 / PAGE_H, width: 408 / PAGE_W, height: 12 / PAGE_H },
  ],
}

function attachParsedInfo(item: StandardProperty): StandardProperty {
  const highlights = demoHighlights[item.id]
  return highlights ? { ...item, parsed: true, highlights } : item
}

function matchPattern(url: string | undefined, prefix: string): string | null {
  if (!url) return null
  const escaped = prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const match = url.match(new RegExp(`^${escaped}/(.+)$`))
  return match ? match[1] : null
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

function hashString(input: string): number {
  let hash = 0
  for (let i = 0; i < input.length; i += 1) {
    hash = (hash * 31 + input.charCodeAt(i)) >>> 0
  }
  return hash
}

function formDataFile(data: unknown): File | null {
  if (data instanceof FormData) {
    const file = data.get('file')
    return file instanceof File ? file : null
  }
  return null
}

function formDataMetadata(data: unknown): StandardPropertyInput | null {
  if (data instanceof FormData) {
    const raw = data.get('metadata')
    if (typeof raw === 'string') {
      try {
        return JSON.parse(raw) as StandardPropertyInput
      } catch {
        return null
      }
    }
  }
  return null
}

function parseJsonBody(data: unknown): Record<string, unknown> {
  if (typeof data === 'string') {
    try {
      return JSON.parse(data) as Record<string, unknown>
    } catch {
      return {}
    }
  }
  return (data && typeof data === 'object' ? data : {}) as Record<string, unknown>
}

function buildAnalysis(standard: StandardProperty): StandardAIAnalysis {
  const existing = standardAIAnalyses.find((a) => a.id === standard.id)
  if (existing) return existing
  return {
    id: standard.id,
    summary: `已解析《${standard.name}》（${standard.code}）：${standard.level}，${standard.nature}性标准，当前状态为「${standard.status}」。`,
    keyPoints: [
      `发布部门：${standard.issuer}，发布年份：${standard.publishYear}。`,
      `所属行业：${standard.industry}，适用于${standard.industry}相关工程与管理场景。`,
      '建议结合项目实际需求对照原文条款执行，并关注后续修订版本。',
    ],
    relatedStandards: [],
    riskWarnings: standard.status === '作废'
      ? ['该标准已作废，使用前请确认现行有效版本。']
      : standard.status === '即将实施'
        ? ['该标准尚未正式实施，请留意正式实施日期及过渡安排。']
        : [],
  }
}

export function registerStandardsMock(
  mock: MockAdapter,
): void {
  mock.onGet('/api/admin/standards').reply((config) => {
    const params = (config.params || {}) as Record<string, unknown>
    let items = [...adminStandards]
    const keyword = String(params.keyword || '').trim().toLowerCase()
    if (keyword) {
      items = items.filter((s) => s.name.toLowerCase().includes(keyword) || s.code.toLowerCase().includes(keyword))
    }
    if (params.industry) items = items.filter((s) => s.industry === params.industry)
    if (params.nature) items = items.filter((s) => s.nature === params.nature)
    if (params.level) items = items.filter((s) => s.level === params.level)
    if (params.status) items = items.filter((s) => s.status === params.status)
    if (params.publishYear) items = items.filter((s) => s.publishYear === Number(params.publishYear))
    const skipCount = Number(params.skipCount || 0)
    const maxResultCount = Number(params.maxResultCount || 15)
    return [200, {
      items: items.slice(skipCount, skipCount + maxResultCount).map(attachParsedInfo),
      totalCount: items.length,
    }]
  })

  mock.onPost('/api/admin/standards/preview').reply(async (config) => {
    const file = formDataFile(config.data)
    if (!file) return [400, {}]
    const baseName = file.name.replace(/\.pdf$/i, '').trim() || '未命名标准'
    const hash = hashString(file.name)
    const industries = ['水利', '建筑', '交通', '环保', '能源', '综合']
    const natures = ['强制', '推荐', '指导']
    const levels = ['国家标准', '行业标准', '地方标准', '团体标准', '企业标准', '国际标准', '法律法规']
    const issuers = ['国务院', '水利部', '住房和城乡建设部', '交通运输部', '生态环境部', '全国人大常委会']
    const statuses = ['现行', '即将实施', '作废']
    const industry = industries[hash % industries.length]
    const nature = natures[hash % natures.length]
    const level = levels[hash % levels.length]
    const issuer = issuers[hash % issuers.length]
    const status = statuses[hash % statuses.length]
    const publishYear = 2000 + (hash % 26)
    const code = `GB/T ${10000 + (hash % 9000)}-${publishYear}`
    const description = `《${baseName}》由 ${issuer} 于 ${publishYear} 年发布，属于${level}、${nature}性标准，适用于${industry}相关工程与管理场景。`
    await delay(1200)
    return [200, { name: baseName, code, industry, nature, level, status, issuer, publishYear, description }]
  })

  mock.onPost('/api/admin/standards').reply(async (config) => {
    const file = formDataFile(config.data)
    const metadata = formDataMetadata(config.data)
    if (!file || !metadata) return [400, {}]
    await delay(1500)
    const record: StandardProperty = {
      id: `std-upload-${Date.now()}`,
      ...metadata,
      parentId: metadata.level ?? undefined,
    }
    adminStandards.unshift(record)
    return [200, record]
  })

  mock.onPost('/api/admin/standards/batch-delete').reply((config) => {
    const body = parseJsonBody(config.data)
    const ids: string[] = Array.isArray(body.ids) ? (body.ids as string[]) : []
    let deletedCount = 0
    for (const id of ids) {
      const idx = adminStandards.findIndex((s) => s.id === id)
      if (idx !== -1) {
        adminStandards.splice(idx, 1)
        deletedCount += 1
      }
    }
    return [200, deletedCount]
  })

  mock.onPost('/api/admin/standards/batch-parse').reply((config) => {
    const body = parseJsonBody(config.data)
    const ids: string[] = Array.isArray(body.ids) ? (body.ids as string[]) : []
    const results: StandardParseBatchResult[] = ids.map((id) => {
      const standard = adminStandards.find((s) => s.id === id)
      if (!standard) return { id, success: false, error: '标准不存在' }
      return { id, success: true, analysis: buildAnalysis(standard) }
    })
    return [200, results]
  })

  mock.onDelete(/\/api\/admin\/standards\/.+$/).reply((config) => {
    const id = matchPattern(config.url, '/api/admin/standards')
    if (!id) return [404, {}]
    const idx = adminStandards.findIndex((s) => s.id === id)
    if (idx === -1) return [404, {}]
    adminStandards.splice(idx, 1)
    return [204]
  })

  mock.onPut(/\/api\/admin\/standards\/.+$/).reply((config) => {
    const id = matchPattern(config.url, '/api/admin/standards')
    if (!id) return [404, {}]
    const idx = adminStandards.findIndex((s) => s.id === id)
    if (idx === -1) return [404, {}]
    const body = parseJsonBody(config.data)
    adminStandards[idx] = { ...adminStandards[idx], ...body, id }
    return [200, adminStandards[idx]]
  })

  mock.onPost(/\/api\/admin\/standards\/.+\/parse$/).reply((config) => {
    const id = matchPattern(config.url, '/api/admin/standards')
    if (!id) return [404, {}]
    const standard = adminStandards.find((s) => s.id === id)
    if (!standard) return [404, {}]
    return [200, buildAnalysis(standard)]
  })
}
```

- [ ] **Step 2: 验证**

运行：`pnpm --filter admin-web run typecheck`

Expected：exit 0。

- [ ] **Step 3: Commit**

```bash
git add admin-web/src/mock/routes/standards.ts
git commit -m "feat(standards): 增加预读、上传、批量删除与批量解析 mock"
```

---

### Task 4: 页面目录迁移与共享常量

**Files:**
- Move: `admin-web/src/views/data/static/standards.vue` → `admin-web/src/views/data/static/standards/index.vue`
- Move: `admin-web/src/views/data/static/components/StandardPdfViewer.vue` → `admin-web/src/views/data/static/standards/components/StandardPdfViewer.vue`
- Create: `admin-web/src/views/data/static/standards/constants.ts`
- Modify: `admin-web/src/router/manifests.ts`

- [ ] **Step 1: 移动文件（先创建目标目录，再逐个移动）**

```powershell
New-Item -ItemType Directory -Force -Path 'admin-web/src/views/data/static/standards/components' | Out-Null
Move-Item -LiteralPath 'admin-web/src/views/data/static/standards.vue' -Destination 'admin-web/src/views/data/static/standards/index.vue'
Move-Item -LiteralPath 'admin-web/src/views/data/static/components/StandardPdfViewer.vue' -Destination 'admin-web/src/views/data/static/standards/components/StandardPdfViewer.vue'
$oldDir = (Resolve-Path -LiteralPath 'admin-web/src/views/data/static/components').Path
$parent = (Resolve-Path -LiteralPath 'admin-web/src/views/data/static').Path
if ($oldDir.StartsWith($parent + [IO.Path]::DirectorySeparatorChar) -and -not (Get-ChildItem -LiteralPath $oldDir -Force)) {
  Remove-Item -LiteralPath $oldDir
}
```

Expected：`admin-web/src/views/data/static/standards.vue` 与旧 `components/` 目录不存在，新目录含 `index.vue`、`components/StandardPdfViewer.vue`。

- [ ] **Step 2: 创建 `constants.ts`**

```ts
/** 标准规范模块共享选项与工具，供页面与上传组件复用 */
export const industryOptions = ['水利', '建筑', '交通', '环保', '能源', '综合']
export const natureOptions = ['强制', '推荐', '指导']
export const levelOptions = ['国家标准', '行业标准', '地方标准', '团体标准', '企业标准', '国际标准', '法律法规']
export const statusOptions = ['现行', '作废', '即将实施']

export const currentYear = new Date().getFullYear()
export const yearOptions = Array.from({ length: currentYear - 1989 }, (_, i) => currentYear - i)

export const industrySelectOptions = industryOptions.map((value) => ({ value, label: value }))
export const natureSelectOptions = natureOptions.map((value) => ({ value, label: value }))
export const levelSelectOptions = levelOptions.map((value) => ({ value, label: value }))
export const statusSelectOptions = statusOptions.map((value) => ({ value, label: value }))
export const yearSelectOptions = yearOptions.map((value) => ({ value, label: String(value) }))

export function statusColor(status?: string): string {
  if (status === '现行') return 'green'
  if (status === '作废') return 'red'
  if (status === '即将实施') return 'blue'
  return 'default'
}
```

- [ ] **Step 3: 更新路由 manifest**

使用 `apply_patch` 将 `admin-web/src/router/manifests.ts` 中：

```ts
component: () => import('@/views/data/static/standards.vue'),
```

改为：

```ts
component: () => import('@/views/data/static/standards/index.vue'),
```

- [ ] **Step 4: 验证**

运行：`pnpm --filter admin-web run typecheck`

Expected：exit 0（移动后 `index.vue` 内的 `./components/StandardPdfViewer.vue` 正好指向新位置 `standards/components/`；若此处因相对路径报错，检查组件是否已移动到位并修正导入路径）。

- [ ] **Step 5: Commit**

```bash
git add -A -- \
  admin-web/src/router/manifests.ts \
  admin-web/src/views/data/static/standards.vue \
  admin-web/src/views/data/static/standards/index.vue \
  admin-web/src/views/data/static/components/StandardPdfViewer.vue \
  admin-web/src/views/data/static/standards/components/StandardPdfViewer.vue \
  admin-web/src/views/data/static/standards/constants.ts
git commit -m "refactor(standards): 页面迁移到目录结构并抽取共享常量"
```

---

### Task 5: 上传队列 composable

**Files:**
- Create: `admin-web/src/views/data/static/standards/composables/useStandardUpload.ts`

- [ ] **Step 1: 创建 `useStandardUpload.ts`**

```ts
import { computed, ref } from 'vue'
import { message } from 'ant-design-vue'
import { previewStandard, uploadStandard } from '@/api/modules/standards'
import type { StandardPropertyInput } from '@/types'

export type UploadTaskStatus =
  | 'previewing'
  | 'ready'
  | 'preview_failed'
  | 'uploading'
  | 'uploaded'
  | 'upload_failed'

export interface StandardUploadTask {
  id: string
  file: File
  fileName: string
  status: UploadTaskStatus
  progress: number
  form: StandardPropertyInput
  error?: string
  standardId?: string
}

export const MAX_UPLOAD_FILES = 10
export const MAX_FILE_SIZE = 50 * 1024 * 1024

let taskSeq = 0

function nextTaskId(): string {
  taskSeq += 1
  return `upload-${Date.now()}-${taskSeq}`
}

export function useStandardUpload(onCompleted?: () => void) {
  const tasks = ref<StandardUploadTask[]>([])
  const timers = new Map<string, ReturnType<typeof setInterval>>()

  const runningCount = computed(() =>
    tasks.value.filter((t) => t.status === 'previewing' || t.status === 'uploading').length,
  )
  const hasTasks = computed(() => tasks.value.length > 0)

  function clearTimer(id: string): void {
    const timer = timers.get(id)
    if (timer) {
      clearInterval(timer)
      timers.delete(id)
    }
  }

  function startProgress(id: string, target = 90, step = 8): void {
    clearTimer(id)
    timers.set(id, setInterval(() => {
      const task = tasks.value.find((t) => t.id === id)
      if (!task) {
        clearTimer(id)
        return
      }
      if (task.progress >= target) {
        clearTimer(id)
        return
      }
      task.progress = Math.min(target, task.progress + step)
    }, 120))
  }

  function patchTask(id: string, patch: Partial<StandardUploadTask>): void {
    const task = tasks.value.find((t) => t.id === id)
    if (task) Object.assign(task, patch)
  }

  function addFiles(files: File[]): void {
    const validFiles = files.filter((file) => {
      if (!/\.pdf$/i.test(file.name)) {
        message.warning(`「${file.name}」仅支持 PDF 文件`)
        return false
      }
      if (file.size > MAX_FILE_SIZE) {
        message.warning(`「${file.name}」超过 50MB 限制`)
        return false
      }
      return true
    })
    const activeCount = tasks.value.filter((t) => t.status !== 'uploaded').length
    const remaining = MAX_UPLOAD_FILES - activeCount
    if (remaining <= 0) {
      message.warning(`单批最多上传 ${MAX_UPLOAD_FILES} 个文件`)
      return
    }
    const accepted = validFiles.slice(0, remaining)
    if (accepted.length < validFiles.length) {
      message.warning(`单批最多上传 ${MAX_UPLOAD_FILES} 个文件，已保留前 ${accepted.length} 个`)
    }
    for (const file of accepted) {
      const task: StandardUploadTask = {
        id: nextTaskId(),
        file,
        fileName: file.name,
        status: 'previewing',
        progress: 0,
        form: { name: '', code: '' },
      }
      tasks.value.push(task)
      void runPreview(task.id)
    }
  }

  async function runPreview(id: string): Promise<void> {
    const task = tasks.value.find((t) => t.id === id)
    if (!task) return
    patchTask(id, { status: 'previewing', progress: 0, error: undefined })
    startProgress(id)
    try {
      const form = await previewStandard(task.file)
      patchTask(id, { status: 'ready', progress: 100, form })
      clearTimer(id)
    } catch {
      patchTask(id, { status: 'preview_failed', error: 'AI 预读失败，请重试' })
      clearTimer(id)
    }
  }

  async function uploadOne(id: string): Promise<void> {
    const task = tasks.value.find((t) => t.id === id)
    if (!task) return
    patchTask(id, { status: 'uploading', progress: 0, error: undefined })
    startProgress(id, 90, 6)
    try {
      const record = await uploadStandard(task.file, task.form)
      patchTask(id, { status: 'uploaded', progress: 100, standardId: record.id })
      clearTimer(id)
    } catch {
      patchTask(id, { status: 'upload_failed', error: '上传失败，请重试' })
      clearTimer(id)
    }
  }

  async function submitUploads(): Promise<void> {
    const invalid = tasks.value.filter(
      (t) => t.status === 'ready' && (!t.form.name?.trim() || !t.form.code?.trim()),
    )
    if (invalid.length) {
      message.warning(`有 ${invalid.length} 个文件未填写名称/编号，请补充后重试`)
      return
    }
    const pending = tasks.value.filter((t) => t.status === 'ready')
    if (!pending.length) return
    await Promise.all(pending.map((t) => uploadOne(t.id)))
    const pendingIds = new Set(pending.map((t) => t.id))
    const successCount = tasks.value.filter((t) => pendingIds.has(t.id) && t.status === 'uploaded').length
    const failedCount = tasks.value.filter((t) => pendingIds.has(t.id) && t.status === 'upload_failed').length
    if (successCount) message.success(`上传完成 ${successCount} 个文件`)
    if (failedCount) message.error(`${failedCount} 个文件上传失败，可在“上传任务”中重试`)
    onCompleted?.()
  }

  function retryTask(id: string): void {
    const task = tasks.value.find((t) => t.id === id)
    if (!task) return
    if (task.status === 'preview_failed') void runPreview(id)
    if (task.status === 'upload_failed') void uploadOne(id)
  }

  function removeTask(id: string): void {
    const task = tasks.value.find((t) => t.id === id)
    if (!task) return
    if (task.status === 'uploading') return
    clearTimer(id)
    tasks.value = tasks.value.filter((t) => t.id !== id)
  }

  function updateForm(id: string, form: StandardPropertyInput): void {
    patchTask(id, { form })
  }

  function dispose(): void {
    timers.forEach((timer) => clearInterval(timer))
    timers.clear()
  }

  return {
    tasks,
    runningCount,
    hasTasks,
    addFiles,
    removeTask,
    retryTask,
    submitUploads,
    updateForm,
    dispose,
  }
}
```

- [ ] **Step 2: 验证**

运行：`pnpm --filter admin-web run typecheck`

Expected：exit 0。

- [ ] **Step 3: Commit**

```bash
git add admin-web/src/views/data/static/standards/composables/useStandardUpload.ts
git commit -m "feat(standards): 上传任务队列 composable"
```

---

### Task 6: 元数据表单组件

**Files:**
- Create: `admin-web/src/views/data/static/standards/components/StandardMetadataForm.vue`

- [ ] **Step 1: 创建 `StandardMetadataForm.vue`**

```vue
<template>
  <a-form layout="vertical" class="standard-metadata-form">
    <a-row :gutter="12">
      <a-col :span="12">
        <a-form-item label="名称" required>
          <a-input v-model:value="form.name" :disabled="disabled" placeholder="请输入标准名称" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="编号" required>
          <a-input v-model:value="form.code" :disabled="disabled" placeholder="请输入标准编号" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="行业">
          <a-select v-model:value="form.industry" :options="industrySelectOptions" :disabled="disabled" allow-clear placeholder="请选择行业" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="性质">
          <a-select v-model:value="form.nature" :options="natureSelectOptions" :disabled="disabled" allow-clear placeholder="请选择性质" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="级别">
          <a-select v-model:value="form.level" :options="levelSelectOptions" :disabled="disabled" allow-clear placeholder="请选择级别" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="状态">
          <a-select v-model:value="form.status" :options="statusSelectOptions" :disabled="disabled" allow-clear placeholder="请选择状态" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="发布部门">
          <a-input v-model:value="form.issuer" :disabled="disabled" placeholder="请输入发布部门" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="发布年份">
          <a-select v-model:value="form.publishYear" :options="yearSelectOptions" :disabled="disabled" allow-clear placeholder="请选择发布年份" />
        </a-form-item>
      </a-col>
    </a-row>
    <a-form-item label="简介">
      <a-textarea v-model:value="form.description" :rows="3" :disabled="disabled" placeholder="请输入标准简介" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { reactive, watch } from 'vue'
import type { StandardPropertyInput } from '@/types'
import {
  industrySelectOptions,
  levelSelectOptions,
  natureSelectOptions,
  statusSelectOptions,
  yearSelectOptions,
} from '../constants'

const props = defineProps<{
  modelValue: StandardPropertyInput
  disabled?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: StandardPropertyInput]
}>()

const form = reactive<StandardPropertyInput>({ ...props.modelValue })
let syncing = false

watch(
  () => props.modelValue,
  (value) => {
    syncing = true
    Object.assign(form, { name: '', code: '', ...value })
    syncing = false
  },
  { immediate: true },
)

watch(
  form,
  () => {
    if (!syncing) emit('update:modelValue', { ...form })
  },
  { deep: true },
)
</script>

<style scoped lang="less">
.standard-metadata-form {
  padding-top: 4px;
}
</style>
```

- [ ] **Step 2: 验证**

运行：`pnpm --filter admin-web run typecheck`

Expected：exit 0。

- [ ] **Step 3: Commit**

```bash
git add admin-web/src/views/data/static/standards/components/StandardMetadataForm.vue
git commit -m "feat(standards): 元数据表单组件"
```

---

### Task 7: 上传弹窗组件

**Files:**
- Create: `admin-web/src/views/data/static/standards/components/StandardUploadModal.vue`

- [ ] **Step 1: 创建 `StandardUploadModal.vue`**

```vue
<template>
  <a-modal
    :open="open"
    title="上传标准文档"
    width="800px"
    :footer="null"
    @cancel="emit('update:open', false)"
  >
    <div class="upload-modal">
      <a-upload-dragger
        multiple
        accept=".pdf,application/pdf"
        :show-upload-list="false"
        :before-upload="handleBeforeUpload"
      >
        <p class="ant-upload-drag-icon">
          <InboxOutlined />
        </p>
        <p class="ant-upload-text">点击或拖拽 PDF 文件到此区域</p>
        <p class="ant-upload-hint">支持 .pdf，单个不超过 50MB，单批最多 10 个</p>
      </a-upload-dragger>

      <a-collapse v-if="tasks.length" v-model:activeKey="activeKey" class="upload-modal__list">
        <a-collapse-panel v-for="task in tasks" :key="task.id">
          <template #header>
            <div class="upload-modal__row">
              <FilePdfOutlined class="upload-modal__icon" />
              <span class="upload-modal__name" :title="task.fileName">{{ task.fileName }}</span>
              <a-tag :color="taskTagColor(task.status)">{{ taskTagText(task.status) }}</a-tag>
              <a-button
                v-if="task.status === 'preview_failed'"
                type="link"
                size="small"
                @click.stop="emit('retry-task', task.id)"
              >
                重试
              </a-button>
              <a-button
                type="text"
                size="small"
                class="upload-modal__remove"
                :disabled="task.status === 'uploading'"
                @click.stop="emit('remove-task', task.id)"
              >
                <DeleteOutlined />
              </a-button>
            </div>
          </template>
          <a-progress v-if="task.status === 'previewing'" :percent="task.progress" size="small" />
          <a-alert
            v-else-if="task.status === 'preview_failed'"
            type="error"
            :message="task.error"
            show-icon
          />
          <StandardMetadataForm
            v-else
            :model-value="task.form"
            @update:model-value="(value) => emit('update-form', task.id, value)"
          />
        </a-collapse-panel>
      </a-collapse>

      <a-empty v-else description="请先选择 PDF 文件" image="simple" />
    </div>

    <div class="upload-modal__footer">
      <span class="upload-modal__tip">AI 预读后请核对元数据，名称和编号必填</span>
      <a-space :size="8">
        <a-button @click="emit('update:open', false)">取消</a-button>
        <a-button type="primary" :disabled="readyCount === 0" @click="emit('submit')">
          上传（{{ readyCount }}）
        </a-button>
      </a-space>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { DeleteOutlined, FilePdfOutlined, InboxOutlined } from '@ant-design/icons-vue'
import StandardMetadataForm from './StandardMetadataForm.vue'
import type { StandardUploadTask, UploadTaskStatus } from '../composables/useStandardUpload'
import type { StandardPropertyInput } from '@/types'

const props = defineProps<{
  open: boolean
  tasks: StandardUploadTask[]
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'add-files': [files: File[]]
  'update-form': [id: string, form: StandardPropertyInput]
  'remove-task': [id: string]
  'retry-task': [id: string]
  'submit': []
}>()

const activeKey = ref<string[]>([])

const readyCount = computed(() => props.tasks.filter((t) => t.status === 'ready').length)

function handleBeforeUpload(file: File): boolean {
  emit('add-files', [file])
  return false
}

function taskTagColor(status: UploadTaskStatus): string {
  if (status === 'previewing' || status === 'uploading') return 'blue'
  if (status === 'ready' || status === 'uploaded') return 'green'
  return 'red'
}

function taskTagText(status: UploadTaskStatus): string {
  switch (status) {
    case 'previewing': return '预读中'
    case 'ready': return '已就绪'
    case 'preview_failed': return '预读失败'
    case 'uploading': return '上传中'
    case 'uploaded': return '已完成'
    case 'upload_failed': return '上传失败'
  }
  return status
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.upload-modal {
  &__list {
    margin-top: @spacing-md;
  }

  &__row {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    min-width: 0;
  }

  &__icon {
    color: @danger;
    flex-shrink: 0;
  }

  &__name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  &__remove {
    flex-shrink: 0;
  }

  &__footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: @spacing-md;
    margin-top: @spacing-md;
    padding-top: @spacing-md;
    border-top: 1px solid @border-color;
  }

  &__tip {
    font-size: @font-size-xs;
    color: @text-tertiary;
  }
}

@media (prefers-reduced-motion: reduce) {
  .upload-modal :deep(.ant-collapse-content) {
    transition: none;
  }
}
</style>
```

- [ ] **Step 2: 验证**

运行：`pnpm --filter admin-web run typecheck`

Expected：exit 0。

- [ ] **Step 3: Commit**

```bash
git add admin-web/src/views/data/static/standards/components/StandardUploadModal.vue
git commit -m "feat(standards): 上传文档弹窗（AI 预填折叠条）"
```

---

### Task 8: 上传任务抽屉组件

**Files:**
- Create: `admin-web/src/views/data/static/standards/components/StandardUploadTasksDrawer.vue`

- [ ] **Step 1: 创建 `StandardUploadTasksDrawer.vue`**

```vue
<template>
  <a-drawer
    :open="open"
    title="上传任务"
    width="520px"
    @close="emit('update:open', false)"
  >
    <a-empty v-if="!tasks.length" description="暂无上传任务" image="simple" />
    <div v-else class="task-drawer">
      <div v-for="task in tasks" :key="task.id" class="task-drawer__item">
        <div class="task-drawer__head">
          <FilePdfOutlined class="task-drawer__icon" />
          <span class="task-drawer__name" :title="task.fileName">{{ task.fileName }}</span>
          <a-tag :color="taskTagColor(task.status)">{{ taskTagText(task.status) }}</a-tag>
          <a-button
            v-if="task.status === 'preview_failed' || task.status === 'upload_failed'"
            type="link"
            size="small"
            @click="emit('retry-task', task.id)"
          >
            重试
          </a-button>
        </div>
        <a-progress
          v-if="task.status === 'previewing' || task.status === 'uploading'"
          :percent="task.progress"
          size="small"
        />
        <a-alert v-if="task.error" type="error" :message="task.error" show-icon />
      </div>
    </div>
  </a-drawer>
</template>

<script setup lang="ts">
import { FilePdfOutlined } from '@ant-design/icons-vue'
import type { StandardUploadTask, UploadTaskStatus } from '../composables/useStandardUpload'

defineProps<{
  open: boolean
  tasks: StandardUploadTask[]
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'retry-task': [id: string]
}>()

function taskTagColor(status: UploadTaskStatus): string {
  if (status === 'previewing' || status === 'uploading') return 'blue'
  if (status === 'ready' || status === 'uploaded') return 'green'
  return 'red'
}

function taskTagText(status: UploadTaskStatus): string {
  switch (status) {
    case 'previewing': return '预读中'
    case 'ready': return '已就绪'
    case 'preview_failed': return '预读失败'
    case 'uploading': return '上传中'
    case 'uploaded': return '已完成'
    case 'upload_failed': return '上传失败'
  }
  return status
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.task-drawer {
  display: flex;
  flex-direction: column;
  gap: @spacing-sm;

  &__item {
    padding: @spacing-sm @spacing-base;
    background: @card-bg;
    border: 1px solid @border-color;
    border-radius: @radius-base;
  }

  &__head {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    min-width: 0;
  }

  &__icon {
    color: @danger;
    flex-shrink: 0;
  }

  &__name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    color: @text-primary;
  }
}
</style>
```

- [ ] **Step 2: 验证**

运行：`pnpm --filter admin-web run typecheck`

Expected：exit 0。

- [ ] **Step 3: Commit**

```bash
git add admin-web/src/views/data/static/standards/components/StandardUploadTasksDrawer.vue
git commit -m "feat(standards): 后台上传任务抽屉"
```

---

### Task 9: 批量解析弹窗与页面类型

**Files:**
- Create: `admin-web/src/views/data/static/standards/types.ts`
- Create: `admin-web/src/views/data/static/standards/components/StandardBatchParseModal.vue`

- [ ] **Step 1: 创建 `types.ts`**

```ts
export interface StandardParseBatchItem {
  id: string
  name: string
  status: 'parsing' | 'success' | 'failed'
  error?: string
}
```

- [ ] **Step 2: 创建 `StandardBatchParseModal.vue`**

```vue
<template>
  <a-modal
    :open="open"
    title="批量解析"
    width="640px"
    :footer="null"
    @cancel="emit('update:open', false)"
  >
    <a-empty v-if="!items.length" description="请先选择标准" image="simple" />
    <div v-else class="batch-parse">
      <div v-for="item in items" :key="item.id" class="batch-parse__row">
        <span class="batch-parse__name" :title="item.name">{{ item.name }}</span>
        <template v-if="item.status === 'parsing'">
          <a-spin size="small" />
          <a-tag color="blue">解析中</a-tag>
        </template>
        <template v-else-if="item.status === 'success'">
          <a-tag color="green">成功</a-tag>
          <a-button type="link" size="small" @click="emit('view', item.id)">查看</a-button>
        </template>
        <template v-else>
          <a-tag color="red">失败</a-tag>
          <a-button type="link" size="small" @click="emit('retry', item.id)">重试</a-button>
          <span class="batch-parse__error" :title="item.error">{{ item.error }}</span>
        </template>
      </div>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import type { StandardParseBatchItem } from '../types'

defineProps<{
  open: boolean
  items: StandardParseBatchItem[]
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'view': [id: string]
  'retry': [id: string]
}>()
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.batch-parse {
  display: flex;
  flex-direction: column;
  gap: @spacing-xs;

  &__row {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    padding: @spacing-xs @spacing-sm;
    background: @card-bg;
    border: 1px solid @border-color;
    border-radius: @radius-base;
  }

  &__name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    color: @text-primary;
  }

  &__error {
    max-width: 220px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: @font-size-xs;
    color: @danger;
  }
}
</style>
```

- [ ] **Step 3: 验证**

运行：`pnpm --filter admin-web run typecheck`

Expected：exit 0。

- [ ] **Step 4: Commit**

```bash
git add admin-web/src/views/data/static/standards/types.ts admin-web/src/views/data/static/standards/components/StandardBatchParseModal.vue
git commit -m "feat(standards): 批量解析进度弹窗"
```

---

### Task 10: 页面集成（index.vue）

**Files:**
- Modify: `admin-web/src/views/data/static/standards/index.vue`

- [ ] **Step 1: 用完整文件内容替换 `index.vue`**

```vue
<template>
  <div class="page-container">
    <PageHeader title="标准规范" description="知识库标准规范的查询、解析与维护">
      <template #extra>
        <a-space :size="8">
          <a-button v-if="uploadHasTasks" size="small" @click="uploadDrawerVisible = true">
            <CloudUploadOutlined />
            上传任务
            <a-badge :count="uploadRunningCount" :offset="[4, -2]" />
          </a-button>
          <a-button size="small" :loading="refreshing" @click="handleRefresh">
            <ReloadOutlined />
            刷新
          </a-button>
          <a-button type="primary" @click="uploadModalVisible = true">
            <UploadOutlined />
            上传文档
          </a-button>
        </a-space>
      </template>
    </PageHeader>

    <a-result v-if="error" status="error" title="加载失败" :sub-title="error">
      <template #extra>
        <a-button type="primary" @click="fetchStandards">重试</a-button>
      </template>
    </a-result>

    <div v-else ref="tableContainerRef" class="standards-table-wrap">
      <div class="standards-filter-bar">
        <a-input
          v-model:value="query.keyword"
          placeholder="搜索名称 / 编号"
          allow-clear
          style="width: 220px"
        />
        <a-select v-model:value="query.industry" allow-clear placeholder="行业" style="width: 120px">
          <a-select-option v-for="opt in industryOptions" :key="opt" :value="opt">{{ opt }}</a-select-option>
        </a-select>
        <a-select v-model:value="query.nature" allow-clear placeholder="性质" style="width: 110px">
          <a-select-option v-for="opt in natureOptions" :key="opt" :value="opt">{{ opt }}</a-select-option>
        </a-select>
        <a-select v-model:value="query.level" allow-clear placeholder="级别" style="width: 130px">
          <a-select-option v-for="opt in levelOptions" :key="opt" :value="opt">{{ opt }}</a-select-option>
        </a-select>
        <a-select v-model:value="query.status" allow-clear placeholder="状态" style="width: 110px">
          <a-select-option v-for="opt in statusOptions" :key="opt" :value="opt">{{ opt }}</a-select-option>
        </a-select>
        <a-select v-model:value="query.publishYear" allow-clear placeholder="发布年份" style="width: 120px">
          <a-select-option v-for="year in yearOptions" :key="year" :value="year">{{ year }}</a-select-option>
        </a-select>
        <a-button size="small" class="standards-filter-bar__reset" @click="handleReset">重置</a-button>
      </div>

      <SectionCard nopad class="standards-table-card">
        <div v-if="selectedRowKeys.length" class="standards-batch-bar">
          <span class="standards-batch-bar__count">已选 {{ selectedRowKeys.length }} 条</span>
          <a-space :size="8">
            <a-button size="small" @click="openBatchParse">批量解析</a-button>
            <a-popconfirm
              title="确定删除选中的标准？"
              description="删除后不可恢复"
              @confirm="handleBatchDelete"
            >
              <a-button size="small" danger>批量删除</a-button>
            </a-popconfirm>
          </a-space>
        </div>
        <a-table
          class="standards-table"
          :data-source="standards"
          :columns="columns"
          :row-selection="rowSelection"
          :pagination="{
            current: page,
            pageSize,
            total,
            showSizeChanger: false,
            showTotal: (t: number) => `共 ${t} 条`,
          }"
          :loading="loading"
          :scroll="{ x: scrollX }"
          row-key="id"
          size="small"
          :locale="{ emptyText: '暂无数据' }"
          @resize-column="handleResizeColumn"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record, index }">
            <template v-if="column.key === 'index'">
              {{ (page - 1) * pageSize + index + 1 }}
            </template>
            <template v-else-if="column.key === 'status'">
              <a-tag :color="statusColor(record.status)">{{ record.status }}</a-tag>
            </template>
            <template v-else-if="column.key === 'action'">
              <div class="action-cell">
                <a-button type="link" size="small" @click="openViewer(record)">查看</a-button>
                <a-button type="link" size="small" @click="openParse(record)">解析</a-button>
                <a-popconfirm title="确认删除该标准？" placement="left" @confirm="handleDelete(record)">
                  <a-button type="link" size="small" danger>删除</a-button>
                </a-popconfirm>
              </div>
            </template>
          </template>
        </a-table>
      </SectionCard>
    </div>

    <a-drawer
      v-model:open="viewerVisible"
      :title="viewerTarget?.name || '标准原文'"
      width="960px"
      :body-style="{ padding: 0 }"
      @close="resetViewer"
    >
      <template #extra>
        <a-button size="small" @click="openEdit(viewerTarget)">
          <InfoCircleOutlined />
          详情 / 编辑
        </a-button>
      </template>
      <div class="viewer-body">
        <StandardPdfViewer
          v-if="viewerTarget"
          :file-url="getStandardFileUrl(viewerTarget.id)"
          :page="viewerPage"
          :highlights="viewerTarget?.highlights ?? []"
          :standard="viewerTarget"
          @update:page="viewerPage = $event"
        />
      </div>
    </a-drawer>

    <a-modal
      v-model:open="parseVisible"
      :title="`AI 解析 - ${parseTarget?.name || ''}`"
      width="640px"
      :footer="null"
      @cancel="resetParse"
    >
      <a-skeleton v-if="parseLoading" :paragraph="{ rows: 6 }" />
      <a-result v-else-if="parseError" status="error" :title="parseError" />
      <div v-else-if="parseResult" class="parse-result">
        <p class="parse-result__summary">{{ parseResult.summary }}</p>
        <div class="parse-result__block">
          <h4 class="parse-result__title">关键要点</h4>
          <ul v-if="parseResult.keyPoints.length" class="parse-result__list">
            <li v-for="point in parseResult.keyPoints" :key="point">{{ point }}</li>
          </ul>
          <a-empty v-else image="simple" description="暂无要点" />
        </div>
        <div class="parse-result__block">
          <h4 class="parse-result__title">风险提示</h4>
          <ul v-if="parseResult.riskWarnings.length" class="parse-result__list parse-result__list--risk">
            <li v-for="warning in parseResult.riskWarnings" :key="warning">{{ warning }}</li>
          </ul>
          <a-empty v-else image="simple" description="暂无风险提示" />
        </div>
      </div>
    </a-modal>

    <a-modal
      v-model:open="editVisible"
      :title="`编辑标准 - ${editTarget?.name || ''}`"
      width="640px"
      ok-text="保存"
      cancel-text="取消"
      :confirm-loading="saving"
      @ok="handleEditSave"
    >
      <a-form v-if="editTarget" layout="vertical" :model="editForm">
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="名称" required>
              <a-input v-model:value="editForm.name" placeholder="请输入标准名称" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="编号" required>
              <a-input v-model:value="editForm.code" placeholder="请输入标准编号" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="行业">
              <a-select v-model:value="editForm.industry" :options="industrySelectOptions" allow-clear placeholder="请选择行业" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="性质">
              <a-select v-model:value="editForm.nature" :options="natureSelectOptions" allow-clear placeholder="请选择性质" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="级别">
              <a-select v-model:value="editForm.level" :options="levelSelectOptions" allow-clear placeholder="请选择级别" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="状态">
              <a-select v-model:value="editForm.status" :options="statusSelectOptions" allow-clear placeholder="请选择状态" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="发布部门">
              <a-input v-model:value="editForm.issuer" placeholder="请输入发布部门" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="发布年份">
              <a-select v-model:value="editForm.publishYear" :options="yearSelectOptions" allow-clear placeholder="请选择发布年份" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="简介">
          <a-textarea v-model:value="editForm.description" :rows="4" placeholder="请输入标准简介" />
        </a-form-item>
      </a-form>
    </a-modal>

    <StandardUploadModal
      v-model:open="uploadModalVisible"
      :tasks="uploadTasks"
      @add-files="addFiles"
      @update-form="updateForm"
      @remove-task="removeTask"
      @retry-task="retryTask"
      @submit="handleUploadSubmit"
    />

    <StandardUploadTasksDrawer
      v-model:open="uploadDrawerVisible"
      :tasks="uploadTasks"
      @retry-task="retryTask"
    />

    <StandardBatchParseModal
      v-model:open="batchParseVisible"
      :items="batchParseItems"
      @view="handleBatchView"
      @retry="retryBatchParseItem"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { message } from 'ant-design-vue'
import {
  CloudUploadOutlined,
  InfoCircleOutlined,
  ReloadOutlined,
  UploadOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import StandardPdfViewer from './components/StandardPdfViewer.vue'
import StandardUploadModal from './components/StandardUploadModal.vue'
import StandardUploadTasksDrawer from './components/StandardUploadTasksDrawer.vue'
import StandardBatchParseModal from './components/StandardBatchParseModal.vue'
import { useStandardUpload } from './composables/useStandardUpload'
import {
  industryOptions,
  industrySelectOptions,
  levelOptions,
  levelSelectOptions,
  natureOptions,
  natureSelectOptions,
  statusColor,
  statusOptions,
  statusSelectOptions,
  yearOptions,
  yearSelectOptions,
} from './constants'
import type { StandardParseBatchItem } from './types'
import type { StandardAIAnalysis, StandardParseBatchResult, StandardProperty } from '@/types'
import {
  deleteStandard,
  deleteStandards,
  getStandardFileUrl,
  getStandards,
  parseStandard,
  parseStandards,
  updateStandard,
} from '@/api/modules/standards'

const pageSize = 15
const page = ref(1)
const total = ref(0)
const loading = ref(false)
const refreshing = ref(false)
const error = ref('')
const standards = ref<StandardProperty[]>([])

const query = reactive({
  keyword: '',
  industry: undefined as string | undefined,
  nature: undefined as string | undefined,
  level: undefined as string | undefined,
  status: '现行' as string | undefined,
  publishYear: undefined as number | undefined,
})

const columnWidths = reactive<Record<string, number>>({
  index: 80,
  name: 240,
  code: 200,
  industry: 100,
  nature: 90,
  level: 120,
  status: 100,
  publishYear: 100,
  action: 180,
})

const columnMinWidths: Record<string, number> = {
  index: 60,
  name: 240,
  code: 140,
  industry: 80,
  nature: 70,
  level: 100,
  status: 80,
  publishYear: 80,
  action: 180,
}

const columns = computed(() => [
  { title: '序号', dataIndex: 'index', key: 'index', width: columnWidths.index, minWidth: columnMinWidths.index, resizable: true },
  { title: '名称', dataIndex: 'name', key: 'name', width: columnWidths.name, minWidth: columnMinWidths.name, resizable: true },
  { title: '编号', dataIndex: 'code', key: 'code', width: columnWidths.code, minWidth: columnMinWidths.code, resizable: true },
  { title: '行业', dataIndex: 'industry', key: 'industry', width: columnWidths.industry, minWidth: columnMinWidths.industry, resizable: true },
  { title: '性质', dataIndex: 'nature', key: 'nature', width: columnWidths.nature, minWidth: columnMinWidths.nature, resizable: true },
  { title: '级别', dataIndex: 'level', key: 'level', width: columnWidths.level, minWidth: columnMinWidths.level, resizable: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: columnWidths.status, minWidth: columnMinWidths.status, resizable: true },
  { title: '发布年份', dataIndex: 'publishYear', key: 'publishYear', width: columnWidths.publishYear, minWidth: columnMinWidths.publishYear, resizable: true },
  { title: '操作', key: 'action', width: columnWidths.action, minWidth: columnMinWidths.action, fixed: 'right', resizable: true },
])

const tableContainerRef = ref<HTMLElement | null>(null)
const containerWidth = ref(0)
let tableResizeObserver: ResizeObserver | undefined

const contentWidth = computed(() =>
  columns.value.reduce((sum, col) => sum + (typeof col.width === 'number' ? col.width : 0), 0),
)
const scrollX = computed(() => Math.max(containerWidth.value, contentWidth.value))

function observeTableWidth(): void {
  if (!tableContainerRef.value) return
  tableResizeObserver = new ResizeObserver((entries) => {
    const width = entries[0]?.contentRect.width
    if (width) containerWidth.value = Math.round(width)
  })
  tableResizeObserver.observe(tableContainerRef.value)
}

function handleResizeColumn(width: number, column: { key?: string }): void {
  const key = column.key
  if (!key || !(key in columnWidths)) return
  const minWidth = columnMinWidths[key] ?? 50
  columnWidths[key] = Math.max(minWidth, Math.round(width))
}

let filterTimer: ReturnType<typeof setTimeout> | undefined

watch(query, () => {
  clearTimeout(filterTimer)
  filterTimer = setTimeout(() => {
    page.value = 1
    fetchStandards()
  }, 300)
}, { deep: true })

async function fetchStandards(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    const res = await getStandards({
      keyword: query.keyword.trim() || undefined,
      industry: query.industry,
      nature: query.nature,
      level: query.level,
      status: query.status,
      publishYear: query.publishYear,
      skipCount: (page.value - 1) * pageSize,
      maxResultCount: pageSize,
    })
    standards.value = res.items
    total.value = res.totalCount
  } catch {
    error.value = '标准列表加载失败，请稍后重试'
  } finally {
    loading.value = false
  }
}

function handleReset(): void {
  query.keyword = ''
  query.industry = undefined
  query.nature = undefined
  query.level = undefined
  query.status = '现行'
  query.publishYear = undefined
}

async function handleRefresh(): Promise<void> {
  refreshing.value = true
  await fetchStandards()
  refreshing.value = false
  if (error.value) {
    message.error('刷新失败')
  } else {
    message.success('已刷新')
  }
}

interface TablePagination {
  current?: number
}

function handleTableChange(paginationInfo: TablePagination): void {
  page.value = paginationInfo.current || 1
  fetchStandards()
}

const viewerVisible = ref(false)
const viewerTarget = ref<StandardProperty | null>(null)
const viewerPage = ref(1)

function openViewer(record: StandardProperty): void {
  viewerTarget.value = record
  viewerPage.value = 1
  viewerVisible.value = true
}

function resetViewer(): void {
  viewerTarget.value = null
  viewerPage.value = 1
}

const editVisible = ref(false)
const editTarget = ref<StandardProperty | null>(null)
const saving = ref(false)
const editForm = reactive<Partial<StandardProperty>>({
  name: '',
  code: '',
  industry: undefined,
  nature: undefined,
  level: undefined,
  status: undefined,
  issuer: undefined,
  publishYear: undefined,
  description: '',
})

function openEdit(target: StandardProperty | null): void {
  if (!target) return
  editTarget.value = target
  editForm.name = target.name
  editForm.code = target.code
  editForm.industry = target.industry
  editForm.nature = target.nature
  editForm.level = target.level
  editForm.status = target.status
  editForm.issuer = target.issuer
  editForm.publishYear = target.publishYear
  editForm.description = target.description
  editVisible.value = true
}

async function handleEditSave(): Promise<void> {
  if (!editTarget.value) return
  if (!editForm.name?.trim() || !editForm.code?.trim()) {
    message.warning('请填写名称和编号')
    return
  }
  saving.value = true
  try {
    const updated = await updateStandard(editTarget.value.id, { ...editForm })
    const idx = standards.value.findIndex((s) => s.id === updated.id)
    if (idx !== -1) standards.value[idx] = updated
    if (viewerTarget.value?.id === updated.id) viewerTarget.value = updated
    editVisible.value = false
    message.success('保存成功')
    fetchStandards()
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

const parseVisible = ref(false)
const parseLoading = ref(false)
const parseError = ref('')
const parseResult = ref<StandardAIAnalysis | null>(null)
const parseTarget = ref<StandardProperty | null>(null)

async function openParse(record: StandardProperty): Promise<void> {
  parseTarget.value = record
  parseResult.value = null
  parseError.value = ''
  parseVisible.value = true
  parseLoading.value = true
  try {
    parseResult.value = await parseStandard(record.id)
  } catch {
    parseError.value = '解析失败，请稍后重试'
  } finally {
    parseLoading.value = false
  }
}

function resetParse(): void {
  parseResult.value = null
  parseError.value = ''
  parseTarget.value = null
}

async function handleDelete(record: StandardProperty): Promise<void> {
  try {
    await deleteStandard(record.id)
    standards.value = standards.value.filter((s) => s.id !== record.id)
    total.value -= 1
    selectedRowKeys.value = selectedRowKeys.value.filter((key) => key !== record.id)
    message.success('删除成功')
    if (standards.value.length === 0 && page.value > 1) {
      page.value -= 1
      await fetchStandards()
    }
  } catch {
    message.error('删除失败')
  }
}

// ── 批量操作 ─────────────────────────────────────────────
const selectedRowKeys = ref<Array<string | number>>([])

const rowSelection = computed(() => ({
  selectedRowKeys: selectedRowKeys.value,
  onChange: (keys: Array<string | number>) => {
    selectedRowKeys.value = keys
  },
}))

async function handleBatchDelete(): Promise<void> {
  const ids = selectedRowKeys.value.map(String)
  if (!ids.length) return
  try {
    const count = await deleteStandards(ids)
    selectedRowKeys.value = []
    message.success(`删除成功 ${count} 条`)
    await fetchStandards()
    if (standards.value.length === 0 && page.value > 1) {
      page.value -= 1
      await fetchStandards()
    }
  } catch {
    message.error('批量删除失败')
  }
}

const batchParseVisible = ref(false)
const batchParseItems = ref<StandardParseBatchItem[]>([])
const batchParseAnalyses = ref<Record<string, StandardAIAnalysis>>({})

function openBatchParse(): void {
  const byId = new Map(standards.value.map((s) => [s.id, s]))
  batchParseItems.value = selectedRowKeys.value.map(String).map((id) => {
    const record = byId.get(id)
    return { id, name: record?.name ?? id, status: 'parsing' as const }
  })
  if (!batchParseItems.value.length) return
  batchParseVisible.value = true
  void runBatchParse()
}

function applyParseResult(result: StandardParseBatchResult): void {
  const item = batchParseItems.value.find((i) => i.id === result.id)
  if (!item) return
  if (result.success) {
    item.status = 'success'
    item.error = undefined
    if (result.analysis) batchParseAnalyses.value[result.id] = result.analysis
  } else {
    item.status = 'failed'
    item.error = result.error
  }
}

async function runBatchParse(): Promise<void> {
  try {
    const results = await parseStandards(batchParseItems.value.map((i) => i.id), applyParseResult)
    const successCount = results.filter((r) => r.success).length
    const failedCount = results.length - successCount
    if (failedCount === 0) message.success(`批量解析完成，共 ${successCount} 条`)
    else message.warning(`批量解析完成：成功 ${successCount} 条，失败 ${failedCount} 条`)
    await fetchStandards()
  } catch {
    message.error('批量解析失败，请重试')
  }
}

async function retryBatchParseItem(id: string): Promise<void> {
  const item = batchParseItems.value.find((i) => i.id === id)
  if (!item) return
  item.status = 'parsing'
  item.error = undefined
  await parseStandards([id], applyParseResult)
}

function handleBatchView(id: string): void {
  const analysis = batchParseAnalyses.value[id]
  const record = standards.value.find((s) => s.id === id)
  if (!analysis || !record) {
    message.warning('未找到解析结果')
    return
  }
  parseTarget.value = record
  parseResult.value = analysis
  parseError.value = ''
  parseVisible.value = true
}

// ── 上传文档 ─────────────────────────────────────────────
const uploadModalVisible = ref(false)
const uploadDrawerVisible = ref(false)

const {
  tasks: uploadTasks,
  runningCount: uploadRunningCount,
  hasTasks: uploadHasTasks,
  addFiles,
  removeTask,
  retryTask,
  submitUploads,
  updateForm,
  dispose: disposeUpload,
} = useStandardUpload(() => {
  void fetchStandards()
})

function handleUploadSubmit(): void {
  uploadModalVisible.value = false
  void submitUploads()
}

onMounted(() => {
  observeTableWidth()
  fetchStandards()
})

onBeforeUnmount(() => {
  tableResizeObserver?.disconnect()
  disposeUpload()
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.page-container :deep(.page-header-left) {
  display: flex;
  align-items: baseline;
  gap: @spacing-sm;
}
.page-container :deep(.page-desc) {
  margin-top: 0;
  color: @text-tertiary;
}
.page-container :deep(.page-header) {
  margin-bottom: @spacing-md;
}

.standards-filter-bar {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  flex-wrap: wrap;
  margin-bottom: @spacing-base;
}
.standards-filter-bar__reset {
  margin-left: auto;
}

.standards-table-wrap {
  min-width: 0;
}

.standards-batch-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: @spacing-md;
  padding: @spacing-sm @spacing-base;
  border-bottom: 1px solid @border-color;

  &__count {
    font-size: @font-size-sm;
    color: @text-secondary;
  }
}

.action-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 2px;
  white-space: nowrap;
}

.viewer-body {
  height: 100%;
  min-height: 0;
  padding: @spacing-md;
}

.parse-result {
  padding: @spacing-md @spacing-lg;
}
.parse-result__summary {
  margin: 0 0 @spacing-lg;
  line-height: 1.7;
  color: @text-primary;
}
.parse-result__block {
  margin-bottom: @spacing-lg;
  &:last-child {
    margin-bottom: 0;
  }
}
.parse-result__title {
  margin: 0 0 @spacing-sm;
  font-size: @font-size-base;
  font-weight: @font-weight-semibold;
  color: @text-primary;
}
.parse-result__list {
  margin: 0;
  padding-left: @spacing-lg;
  color: @text-secondary;
  line-height: 1.7;
  li + li {
    margin-top: @spacing-xs;
  }
  &--risk {
    color: @danger;
  }
}
</style>
```

- [ ] **Step 2: 验证**

运行：`pnpm --filter admin-web run typecheck`

Expected：exit 0。

- [ ] **Step 3: Commit**

```bash
git add admin-web/src/views/data/static/standards/index.vue
git commit -m "feat(standards): 页面集成批量操作与上传文档"
```

---

### Task 11: 全链路验证与收尾

**Files:**
- 无（验证）

- [ ] **Step 1: 全仓 typecheck**

运行：`pnpm run typecheck`

Expected：exit 0。

- [ ] **Step 2: 手动验证上传链路**

运行：`pnpm --filter admin-web run dev`，打开管理端「知识库 → 标准规范」。

Expected：
1. 点击「上传文档」，选择 2~3 个 PDF；每条折叠条显示文件名 + “预读中”进度，约 1.2s 后变“已就绪”且展开可编辑（字段已自动填写）。
2. 修改其中一条名称后点击「上传（N）」；弹窗收起，PageHeader 出现“上传任务”Badge。
3. 打开“上传任务”抽屉，看到逐条“上传中 → 已完成”；全部完成后出现 message，列表自动刷新且新记录出现在顶部（查看入口会指向 `/mock/standards/<上传id>.pdf`，但 mock 上传不生成实际 PDF，PDF 可能无法加载，属预期内；仅验证入口与路径）。

- [ ] **Step 3: 手动验证失败与重试**

Expected：
1. 选择非 PDF 文件或超过 50MB 文件被拒绝并提示。
2. 选择超过 10 个文件只保留前 10 个并提示。
3. 把 `preview` / 上传 mock 临时改为返回 500（改完记得还原），验证“预读失败/上传失败”状态、抽屉内重试按钮与完成汇总提示。

- [ ] **Step 4: 手动验证批量解析与批量删除**

Expected：
1. 勾选 2 条以上，出现“已选 N 条”操作栏；未勾选时按钮区不显示。
2. 点击「批量解析」：弹窗逐条显示“解析中 → 成功/失败”；成功行点“查看”打开 AI 解析详情；关闭后列表刷新。
3. 点击「批量删除」：确认后删除所选，列表刷新；删除失败时勾选保留。

- [ ] **Step 5: 最终检查工作区**

运行：`git status --short`

Expected：本计划相关文件已提交；确认没有误 stage 与本计划无关的既有改动。若有 `docs/superpowers/plans/2026-08-15-admin-standard-batch-upload.md` 未提交，单独提交：

```bash
git add docs/superpowers/plans/2026-08-15-admin-standard-batch-upload.md
git commit -m "docs: 标准规范批量操作与上传文档实现计划"
```

---

## 自检记录

写完计划后核对：

- **Spec 覆盖**：上传弹窗（折叠条 + AI 预填 + 编辑）→ Task 5/6/7；后台任务与页面提示 → Task 8/10；批量解析进度弹窗与“查看” → Task 9/10；批量删除 → Task 10；共享类型/URL/API/mock → Task 1/2/3；错误处理与动效降级 → Task 5/7/10；验证 → Task 11。
- **占位符**：无 TBD/TODO。
- **类型一致性**：`StandardPropertyInput`、`StandardParseBatchResult`、`StandardUploadTask`、`StandardParseBatchItem` 在各任务中的字段名一致；事件名 `add-files` / `update-form` / `remove-task` / `retry-task` / `view` / `retry` 在组件与页面中一致。
