# 比标模块前端 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 user-web 实现「AI投标-比标模块」前端原型：任务列表 → 创建任务(上传) → 条款确认 → 分析进度 → 结果工作台（概览/热力图/证据清单/条款响应矩阵/指标比选）→ 左右 bbox 对比视图 → 报告导出，全部走前端 mock，不依赖后端。

**Architecture:** 严格按 AGENTS.md §2.0 九步清单：shared types → shared urls → shared mock data → user-web api module → user-web mock routes → mock 注册 → 路由（manifests 已占位，不变）→ 页面 → typecheck。页面全部挂在既有路由 `/ai-bid/compare` 下，`compare/index.vue` 用组件内 `view` 状态机（`'list'|'create'|'clauses'|'progress'|'result'|'diff'`）切换六个视图，不新增路由记录；`index.vue` 是唯一持有业务状态、唯一调用 API 的组件，子组件 props down / events up（AGENTS §2.13）。

**Tech Stack:** Vue 3 `<script setup lang="ts">`、ant-design-vue、echarts + vue-echarts（heatmap）、pinia（本模块不新增 store）、pdfjs-dist（新增依赖，PDF 渲染 + bbox 覆盖层）、katex（新增依赖，公式块 LaTeX 渲染）、axios-mock-adapter（mock）、LESS（`@shared/web/styles/variables.less` 变量）。

**假设（已拍板）:**

- 前端 mock 先行：所有 API 走 axios-mock-adapter，mock 数据按 spec §6 契约构造；不依赖任何后端。契约唯一事实源 = `docs/superpowers/specs/2026-07-29-ai-bid-compare-design.md`，字段名逐字遵守 §6.1（`CompareTask/Clause/Evidence/CompareReport`）。**IR 部分例外（2026-08-07 v2 更新）**：spec §4 的 ir.json 跨系统交付契约已废止，`IrDocument` 等类型为 DredgeAI 内部适配形态，按 `docs/superpowers/plans/dredgeai-consume-angineer-requirements.md`（下称 v2 文档）构造——bbox 为 0~1 归一化坐标、`blockId` 直接采用 AnGIneer `block_uid`、`source/confidence` 允许 null。
- 新增依赖仅 `pdfjs-dist` 与 `katex`（`pnpm --filter user-web add pdfjs-dist katex`），pdfjs worker 用 `import workerSrc from 'pdfjs-dist/build/pdf.worker.min.mjs?url'` 方式接入（vite 原生支持 `?url`，`env.d.ts` 已含 `vite/client` 类型，无需改 vite.config.ts）。其余一律用现有依赖。
- **公式渲染用 KaTeX**（AnGIneer 建议）：公式块 LaTeX（v2 §2 `math_content`/`formula_body`）经 `renderLatex` 薄封装渲染，`throwOnError: false` + `strict: 'ignore'`——OCR 坏串渲染为红字原文，不炸组件。**待 AnGIneer 钉死的契约**：math_content 是否裸公式（不含 `$`/`$$` 定界符），需写入 v2 文档 §2；本计划按「裸公式」实现，DocViewer md 管线只渲染显式带 `$$...$$`/`$...$` 定界符的文本（通用 markdown 语义，不影响既有模块）。字体随 `katex/dist/katex.min.css` 引入，颜色继承主题文字色（dark/light 无碍）；katex 体积约 70KB gz，先在 katex.ts 静态引入，后续在意首屏再改动态 import。
- `docs/chart-conventions.md` 在仓库中**不存在**（AGENTS §2.10 引用落空）。热力图遵循从现有代码提炼的图表惯例：`ChartContainer` 容器 + `useChartTheme()` 取 axis/tooltip/legend 色 + `useCssVar()` 取品牌/语义色 + `animationDuration: 600 / easeOutQuad`。若后续该文档补齐，以其为准回查热力图。
- spec §6 只定义了 `POST /export` 异步句柄，未给轮询端点；前端补充 `GET /compare/tasks/:id/export/:exportId` 作为句柄状态轮询（与「导出异步化」决策一致，后端落地时对齐）。`CompareReport` 扩展 `clauseResponses`/`indicatorRows` 两个字段承载条款响应矩阵与指标比选数据（报告 JSON 内容本由后端模板自定，前端先行提案）。
- 演示 PDF 由仓库内脚本生成（`user-web/scripts/gen-compare-sample-pdf.mjs`，无第三方依赖，手写最小 PDF 结构，中文走 STSong-Light + UniGB-UCS2-H 预定义 CMap，pdf.js 回退系统中文字体渲染），产物提交到 `user-web/public/mock/compare/`。页面尺寸 595×842 pt，与 mock IR 的 `pages[].width/height` 一致；mock IR 的 bbox 为 0~1 归一化值（文本行坐标 ÷ 页面尺寸，v2 契约，PDF_Viewer 直接还原，不做像素换算）。**移植 PDF_Viewer 后该脚本仍保留**：bbox 对齐精度走查需要「坐标已知的 PDF + 对应的 mock IR」这一组对照数据。
- **对比视图基于 AnGIneer docs-ui 的 PDF_Viewer 移植（路线 A：复制改造，不做独立包）**。源：`D:/AI/AnGIneer/packages/docs-ui/src/components/common/viewers/PDF_Viewer.vue`（1824 行，含 ~1000 行 `PdfViewerController`：pdfjs Range 流式→ArrayBuffer→iframe 三级降级、虚拟滚动、缩放/fit、页码跳转、防渲染竞态）+ `D:/AI/AnGIneer/packages/docs-ui/src/composables/useWorkspaceLinkage.ts` 中的纯函数坐标归一化段。移植到 `packages/shared/src/web/components/pdf-viewer/`，保持原代码结构不大改（便于日后与 AnGIneer 同步 bugfix），仅做解耦与主题适配：① `KnowledgeTreeNode` 类型 → 本地精简接口；② workspace 形态 props（isPdf/isOffice/isImage/isText/textContent/parse 进度）→ 纯 PDF 精简 props，fileUrl 由外部完整传入（替代 `useWorkspacePreview.ts:59` 的 `/api/files?path=` 拼接）；③ 删除 Office/图片/文本预览分支与解析进度条；④ 主题变量 `--dp-*` → DredgeAI CSS 变量，高亮硬编码蓝 `rgba(24,144,255)` → 支持按配对/严重度着色（经 inline style 注入，严重度色值：高 `#EF4444` / 中 `#F59E0B` / 低 `#3B82F6`）。`useTheme/appClass`（@angineer/ui-kit）只存在于不移植的父组件 PDFParsedWorkspace.vue，PDF_Viewer 本身无此耦合。
- 无招标文件时任务解析完直接进入查重；有招标文件时停在 `parsed` 等待条款锁定（spec §3.2「条款必须用户确认后锁定」）。P1 阶段无条款确认页，进度页在 `parsed` 时自动提取并锁定条款草案（代码注释标明 P2 替换），P2 改为人工确认页。

**验证方式说明:** 项目无测试框架（无 vitest），验证 = 每个 Task 末尾在**仓库根**执行 `pnpm run typecheck` + 启动 `pnpm dev` 的手动走查清单（具体到点哪里、看到什么）。这是对本项目 TDD 惯例的务实替代。

## 命名总表（跨 Task 一致性锚点，后文不得偏离）

- 类型（`packages/shared/src/core/types/compare.ts`）：`CompareTaskStatus`、`CompareStageProgress`、`Clause`、`CompareTask`、`CompareDocument`、`CompareTaskDetail`、`EvidenceLocation`、`Evidence`、`SimilarityMatrix`、`CompareReport`、`ClauseTemplate`、`ClauseResponseStatus`、`ClauseResponse`、`IndicatorRow`、`ExportJob`、`IrPage`、`IrBlockType`、`IrBlock`、`IrOutlineNode`、`IrDocumentMeta`、`IrDocument`
- API 函数（`user-web/src/api/modules/compare.ts`）：`getCompareTasks`、`createCompareTask`、`uploadCompareDocument`、`getCompareTask`、`deleteCompareTask`、`getCompareEvidences`、`getCompareMatrix`、`getCompareIr`、`getCompareReport`、`extractCompareClauses`、`lockCompareClauses`、`getClauseTemplates`、`createClauseTemplate`、`exportCompareReport`、`getCompareExportStatus`
- 页面常量（`user-web/src/views/ai-bid/compare/constants.ts`）：`EVIDENCE_TYPE_LABELS`、`SEVERITY_LABELS`、`SEVERITY_COLORS`、`TASK_STATUS_LABELS`、`TASK_STATUS_COLORS`、`CompareView`
- 组件（`user-web/src/views/ai-bid/compare/components/`）：`TaskList.vue`、`TaskCreate.vue`、`AnalysisProgress.vue`、`ResultWorkbench.vue`、`EvidenceTable.vue`、`ClauseConfirm.vue`、`ClauseMatrix.vue`、`IndicatorTable.vue`、`DiffViewer.vue`
- 移植的共享 PDF 查看器（`packages/shared/src/web/components/pdf-viewer/`）：`PdfViewer.vue`（移植自 AnGIneer `PDF_Viewer.vue`，精简 props 后导出）、`highlight.ts`（导出 `LinkedHighlight`、`RectBounds`、`normalizeRect`、`normalizeRectFromBaseRow`、`normalizeRectFromPayload`、`mapIrBlocksToHighlights`）
- 公式渲染（`packages/shared/src/web/components/katex.ts`）：`renderLatex(tex, displayMode)`；`DocViewer.vue` md 管线追加 `$$...$$`/`$...$` 处理（既有共享组件，向后兼容）
- mock 数据导出（`packages/shared/src/mock/data/compare.ts`）：`compareTaskDetails`、`compareEvidences`、`compareMatrix`、`compareReport`、`compareClauseTemplates`、`compareExtractedDraft`、`compareIrMap`、`compareDocPool`

## spec §7 页面/区块 → Task 映射

| spec §7 条目 | Task |
|---|---|
| 7.1.1 任务列表页 | Task 6【P1】 |
| 7.1.2 创建任务（上传） | Task 7【P1】 |
| 7.1.3 条款确认页 | Task 14【P2】 |
| 7.1.4 分析进度页 | Task 8【P1】 |
| 7.1.5 结果工作台 · 概览（MetricCard + 热力图） | Task 9【P1】 |
| 7.1.5 结果工作台 · 证据清单 | Task 9【P1】 |
| 7.1.5 结果工作台 · 条款响应矩阵 | Task 15【P2】 |
| 7.1.5 结果工作台 · 指标比选表 | Task 15【P2】 |
| 7.1.6 左右对比视图（移植 PDF_Viewer + bbox） | Task 10/11/12/13【P1】 |
| 报告导出（异步 + 轮询） | Task 16【P2】 |
| §9 OCR 低置信醒目提示 | Task 9【P1】（概览 alert） |
| §9 AI 暂不可用降级 | Task 8【P1】（进度页 evidence 先到先展示） |

---

## Task 1 【P1】共享类型定义

**Files:**
- Create: `packages/shared/src/core/types/compare.ts`
- Modify: `packages/shared/src/core/types/index.ts`

- [ ] **Step 1: 创建 `packages/shared/src/core/types/compare.ts`，内容如下（完整文件）**

```ts
/**
 * 比标模块类型定义。
 * 契约来源：docs/superpowers/specs/2026-07-29-ai-bid-compare-design.md §6，
 * CompareTask / Clause / Evidence / CompareReport 字段名逐字对齐 spec §6.1。
 * IR 部分（IrDocument 等）为 DredgeAI 内部适配形态，字段映射遵循
 * docs/superpowers/plans/dredgeai-consume-angineer-requirements.md（2026-08-07 v2）：
 * bbox 0~1 归一化、blockId=AnGIneer block_uid、source/confidence 允许 null；
 * spec §4 的 ir.json 跨系统交付契约已由 v2 取代。
 * 后端落地前，本文件 + mock 数据为联调基准。
 */

/** 任务状态机（spec §3.1）：parsing→parsed→(待条款确认)→comparing→analyzing→done */
export type CompareTaskStatus = 'parsing' | 'parsed' | 'comparing' | 'analyzing' | 'done' | 'failed' | 'partial'

/** 各阶段进度（GET /tasks/{id} 的 progress 字段元素） */
export interface CompareStageProgress {
  stage: 'parsing' | 'comparing' | 'clauseCheck' | 'aiAnalysis'
  label: string
  status: 'wait' | 'process' | 'finish' | 'error'
  /** 0-100 */
  percent: number
}

/** 强制性条款（spec §6.1） */
export interface Clause {
  clauseId: string
  source: 'extracted' | 'manual' | 'template'
  text: string
  mandatory: boolean
  category: string
}

/** 比标任务（spec §6.1）。highRiskCount 为列表 DTO 扩展字段（列表页「高风险数」列） */
export interface CompareTask {
  id: string
  name: string
  status: CompareTaskStatus
  docIds: string[]
  tenderDocId?: string
  clauseSnapshot: Clause[]
  progress: CompareStageProgress[]
  createdAt: string
  highRiskCount?: number
}

/** 任务内文档（标书/招标文件，POST /tasks/{id}/documents 的 role 区分） */
export interface CompareDocument {
  id: string
  taskId: string
  role: 'bid' | 'tender'
  fileName: string
  /** 列表/热力图轴标签用短名，如「标书A」 */
  shortName: string
  pageCount: number
  parseStatus: 'parsing' | 'done' | 'failed'
  /** 解析失败原因（spec §9 单份失败降级，支持单独重传） */
  failReason?: string
  /** OCR 低置信页占比 0~1，>0.3 时概览区醒目提示（spec §4.5/§9）；AnGIneer 补齐 source/confidence 前恒 0（v2 §4 提示降级关闭） */
  ocrLowConfidenceRatio: number
  /** 原始文件访问地址（前端 pdf.js 渲染用；mock 指向 /mock/compare/*.pdf） */
  fileUrl: string
}

/** 任务详情 = 任务 + 文档清单 */
export interface CompareTaskDetail extends CompareTask {
  documents: CompareDocument[]
}

/** 证据定位（spec §6.1 locations 元素） */
export interface EvidenceLocation {
  docId: string
  blockIds: string[]
}

/** 证据项（spec §6.1，全系统核心数据结构） */
export interface Evidence {
  id: string
  taskId: string
  type: 'similarity' | 'pricing' | 'metadata' | 'clause' | 'indicator'
  severity: 'high' | 'mid' | 'low'
  docIds: string[]
  locations: EvidenceLocation[]
  metrics: { similarity?: number }
  title: string
  description: string
  aiGenerated: boolean
}

/** 两两相似度矩阵（GET /matrix，热力图用） */
export interface SimilarityMatrix {
  docIds: string[]
  /** N×N，对角线为 1 */
  values: number[][]
}

/** 结构化报告（spec §6.1；clauseResponses / indicatorRows 为前端提案扩展字段） */
export interface CompareReport {
  taskId: string
  summary: string
  matrix: SimilarityMatrix
  sections: {
    key: 'similarityRisk' | 'clauseResponse' | 'indicatorCompare'
    title: string
    items: string[]
  }[]
  /** 条款响应矩阵数据（行=条款，列=标书） */
  clauseResponses: ClauseResponse[]
  /** 指标比选表数据 */
  indicatorRows: IndicatorRow[]
  generatedAt: string
}

/** 个人条款库模板 */
export interface ClauseTemplate {
  clauseId: string
  text: string
  category: string
  mandatory: boolean
  createdAt: string
}

/** 条款响应状态（条款响应矩阵单元格） */
export type ClauseResponseStatus = 'compliant' | 'partial' | 'noncompliant'

export interface ClauseResponse {
  clauseId: string
  docId: string
  status: ClauseResponseStatus
  /** AI 判定理由 */
  reason: string
  /** 原文定位块 */
  blockIds: string[]
}

/** 指标比选行 */
export interface IndicatorRow {
  indicator: string
  values: { docId: string, summary: string }[]
}

/** 导出任务句柄（spec §6.2 导出异步化，前端轮询获取下载链接） */
export interface ExportJob {
  exportId: string
  format: 'pdf' | 'word'
  status: 'processing' | 'done' | 'failed'
  downloadUrl?: string
}

// ─── IR（DredgeAI 内部适配形态：按 v2 文档 §2/§3 从 AnGIneer doc_blocks_graph 映射，后端存储内部 IR） ───

export interface IrPage {
  pageIdx: number
  /** 页面真实尺寸（AnGIneer meta `pages`，v2 §1）；bbox 已归一化，本字段不参与坐标换算 */
  width: number
  height: number
}

/** seal 为保留类型（spec §4.3.5 印章单独成块）：AnGIneer 当前不产出（v2 §3 映射表无此项） */
export type IrBlockType = 'title' | 'para' | 'table' | 'list' | 'image' | 'equation' | 'seal' | 'header' | 'footer'

export interface IrBlock {
  /** 直接采用 AnGIneer block_uid（如 `doc-406e43e8:3:1`，唯一稳定，v2 §2），不自造 id */
  blockId: string
  pageIdx: number
  /** 0~1 归一化坐标 [x0,y0,x1,y1]，左上角原点（v2 §2；PDF_Viewer 直接还原，不做像素换算） */
  bbox: [number, number, number, number]
  type: IrBlockType
  /** plain_text；公式块用 math_content / formula_body（LaTeX，v2 §2；UI 经 KaTeX 渲染，见 Task 8 Step 3） */
  text: string
  /** 标题块 = derived_level；非标题块固定 0（v2 §2） */
  textLevel: number
  /** AnGIneer 补齐前允许 null（v2 §4；为 null 时 OCR 降权与低置信提示降级关闭） */
  source: 'native' | 'ocr' | null
  /** 同 source 允许 null；存在时 native 恒 1.0 */
  confidence: number | null
  table?: { html: string, imgPath: string }
  imgPath?: string
}

export interface IrOutlineNode {
  title: string
  level: number
  blockId: string
  children: IrOutlineNode[]
}

export interface IrDocumentMeta {
  fileName: string
  pageCount: number
  /** 提取不到给 null，不省略字段（元数据比对要用） */
  author: string | null
  creatorTool: string | null
  createdAt: string | null
  modifiedAt: string | null
}

export interface IrDocument {
  schemaVersion: string
  docId: string
  meta: IrDocumentMeta
  pages: IrPage[]
  outline: IrOutlineNode[]
  blocks: IrBlock[]
}
```

- [ ] **Step 2: 修改 `packages/shared/src/core/types/index.ts`，在末尾追加一行**

```ts
export * from './compare'
```

- [ ] **Step 3: 验证** — 仓库根执行 `pnpm run typecheck`，应通过。本 Task 纯类型，无界面走查。
- [ ] **Step 4: （可选，由执行者决定）** `git add -A && git commit -m "feat(shared): add compare module types"`

---

## Task 2 【P1】URL 契约声明

**Files:**
- Modify: `packages/shared/src/core/api/urls.ts`

- [ ] **Step 1: 在 `urls` 对象中 `bidDocument: '/bid/document',` 之后插入以下 key（完整片段）**

```ts
  // AI 投标 · 比标（契约见 spec §6；compareTaskExportStatus 为前端补充的导出句柄轮询端点）
  compareTasks: '/compare/tasks',
  compareTask: '/compare/tasks/:id',
  compareTaskDocuments: '/compare/tasks/:id/documents',
  compareTaskIr: '/compare/tasks/:id/ir/:docId',
  compareTaskEvidences: '/compare/tasks/:id/evidences',
  compareTaskReport: '/compare/tasks/:id/report',
  compareTaskExport: '/compare/tasks/:id/export',
  compareTaskExportStatus: '/compare/tasks/:id/export/:exportId',
  compareTaskMatrix: '/compare/tasks/:id/matrix',
  compareClauseTemplates: '/compare/clause-templates',
  compareTaskClausesExtract: '/compare/tasks/:id/clauses/extract',
  compareTaskClauses: '/compare/tasks/:id/clauses',
```

- [ ] **Step 2: 验证** — `pnpm run typecheck` 通过。无界面走查。
- [ ] **Step 3: （可选）** `git add -A && git commit -m "feat(shared): add compare api urls"`

---

## Task 3 【P1】mock 数据（演示数据集）

**Files:**
- Create: `packages/shared/src/mock/data/compare.ts`

演示数据集：1 份招标文件 + 3 份标书（A/B/C），3×3 相似度矩阵，6 条证据（similarity 高/中、pricing、metadata、clause、indicator，带 blockIds 定位），5 条强制性条款，3 个任务（done / analyzing / partial）。页面尺寸统一 595×842（与 Task 10 生成的演示 PDF 对齐；bbox 为 0~1 归一化值 = 文本行坐标 ÷ 页面尺寸，v2 契约；blockId 采用 AnGIneer block_uid 风格 `{docId}:{pageIdx}:{序号}`）。

- [ ] **Step 1: 创建 `packages/shared/src/mock/data/compare.ts`，内容如下（完整文件）**

```ts
import type {
  Clause,
  ClauseTemplate,
  CompareDocument,
  CompareReport,
  CompareTaskDetail,
  Evidence,
  IrDocument,
  SimilarityMatrix,
} from '@shared/types'

// ─── 文档（1 招标文件 + 3 标书） ───

const cmp1Documents: CompareDocument[] = [
  { id: 'doc-tender', taskId: 'cmp-1', role: 'tender', fileName: '智慧航道疏浚工程招标文件.pdf', shortName: '招标文件', pageCount: 1, parseStatus: 'done', ocrLowConfidenceRatio: 0, fileUrl: '/mock/compare/tender.pdf' },
  { id: 'doc-a', taskId: 'cmp-1', role: 'bid', fileName: '中港疏浚有限公司投标文件.pdf', shortName: '标书A', pageCount: 3, parseStatus: 'done', ocrLowConfidenceRatio: 0.05, fileUrl: '/mock/compare/bid-a.pdf' },
  // 标书B 低置信页占比 42%：演示 spec §9「扫描件查重结果可能偏差」醒目提示
  { id: 'doc-b', taskId: 'cmp-1', role: 'bid', fileName: '长江航道工程局投标文件.pdf', shortName: '标书B', pageCount: 3, parseStatus: 'done', ocrLowConfidenceRatio: 0.42, fileUrl: '/mock/compare/bid-b.pdf' },
  { id: 'doc-c', taskId: 'cmp-1', role: 'bid', fileName: '海工建设集团投标文件.pdf', shortName: '标书C', pageCount: 3, parseStatus: 'done', ocrLowConfidenceRatio: 0.02, fileUrl: '/mock/compare/bid-c.pdf' },
]

const cmp2Documents: CompareDocument[] = [
  { id: 'cmp2-doc-a', taskId: 'cmp-2', role: 'bid', fileName: '港湾建设投标文件.pdf', shortName: '标书A', pageCount: 3, parseStatus: 'done', ocrLowConfidenceRatio: 0.03, fileUrl: '/mock/compare/bid-a.pdf' },
  { id: 'cmp2-doc-b', taskId: 'cmp-2', role: 'bid', fileName: '远洋疏浚投标文件.pdf', shortName: '标书B', pageCount: 3, parseStatus: 'done', ocrLowConfidenceRatio: 0.08, fileUrl: '/mock/compare/bid-b.pdf' },
]

const cmp3Documents: CompareDocument[] = [
  { id: 'cmp3-doc-a', taskId: 'cmp-3', role: 'bid', fileName: '宏基工程投标文件.pdf', shortName: '标书A', pageCount: 3, parseStatus: 'done', ocrLowConfidenceRatio: 0.04, fileUrl: '/mock/compare/bid-a.pdf' },
  // spec §9：单份解析失败 → 任务降级为「部分完成」，标注原因
  { id: 'cmp3-doc-b', taskId: 'cmp-3', role: 'bid', fileName: '润通港航投标文件.pdf', shortName: '标书B', pageCount: 0, parseStatus: 'failed', failReason: '解析服务超时，可重传重解析', ocrLowConfidenceRatio: 0, fileUrl: '' },
]

// ─── 任务（done / analyzing / partial 三态演示） ───

export const compareTaskDetails: CompareTaskDetail[] = [
  {
    id: 'cmp-1',
    name: '智慧航道疏浚工程比标',
    status: 'done',
    docIds: ['doc-a', 'doc-b', 'doc-c'],
    tenderDocId: 'doc-tender',
    clauseSnapshot: [
      { clauseId: 'cl-1', source: 'extracted', text: '投标人须具备疏浚工程专业承包一级资质', mandatory: true, category: '资质要求' },
      { clauseId: 'cl-2', source: 'extracted', text: '项目经理须具备港口与航道工程一级建造师资格', mandatory: true, category: '人员要求' },
      { clauseId: 'cl-3', source: 'extracted', text: '质保期不少于 2 年', mandatory: true, category: '商务条款' },
      { clauseId: 'cl-4', source: 'extracted', text: '须提供安全生产许可证且在有效期内', mandatory: true, category: '资质要求' },
      { clauseId: 'cl-5', source: 'template', text: '投标保证金须于截止日前 3 个工作日缴纳', mandatory: false, category: '商务条款' },
    ],
    progress: [
      { stage: 'parsing', label: '文档解析', status: 'finish', percent: 100 },
      { stage: 'comparing', label: '两两查重', status: 'finish', percent: 100 },
      { stage: 'clauseCheck', label: '条款校验', status: 'finish', percent: 100 },
      { stage: 'aiAnalysis', label: 'AI 分析', status: 'finish', percent: 100 },
    ],
    createdAt: '2026-07-25 10:24',
    highRiskCount: 3,
    documents: cmp1Documents,
  },
  {
    id: 'cmp-2',
    name: '港区护岸修复工程比标',
    status: 'analyzing',
    docIds: ['cmp2-doc-a', 'cmp2-doc-b'],
    clauseSnapshot: [],
    progress: [
      { stage: 'parsing', label: '文档解析', status: 'finish', percent: 100 },
      { stage: 'comparing', label: '两两查重', status: 'finish', percent: 100 },
      { stage: 'clauseCheck', label: '条款校验', status: 'finish', percent: 100 },
      { stage: 'aiAnalysis', label: 'AI 分析', status: 'process', percent: 45 },
    ],
    createdAt: '2026-07-28 15:02',
    highRiskCount: 1,
    documents: cmp2Documents,
  },
  {
    id: 'cmp-3',
    name: '锚地疏浚维护比标',
    status: 'partial',
    docIds: ['cmp3-doc-a', 'cmp3-doc-b'],
    clauseSnapshot: [],
    progress: [
      { stage: 'parsing', label: '文档解析', status: 'error', percent: 55 },
      { stage: 'comparing', label: '两两查重', status: 'finish', percent: 100 },
      { stage: 'clauseCheck', label: '条款校验', status: 'wait', percent: 0 },
      { stage: 'aiAnalysis', label: 'AI 分析', status: 'wait', percent: 0 },
    ],
    createdAt: '2026-07-27 09:41',
    highRiskCount: 1,
    documents: cmp3Documents,
  },
]

// ─── 证据项（6 条：similarity 高/中、pricing、metadata、clause、indicator） ───

export const compareEvidences: Evidence[] = [
  {
    id: 'ev-1',
    taskId: 'cmp-1',
    type: 'similarity',
    severity: 'high',
    docIds: ['doc-a', 'doc-b'],
    locations: [
      { docId: 'doc-a', blockIds: ['doc-a:1:2', 'doc-a:1:3', 'doc-a:1:4'] },
      { docId: 'doc-b', blockIds: ['doc-b:1:2', 'doc-b:1:3', 'doc-b:1:4'] },
    ],
    metrics: { similarity: 0.87 },
    title: '技术方案章节大面积雷同',
    description: '两份标书第三章技术方案 3.1~3.3 节文本逐字一致（含相同用词与标点），块级对齐相似度 0.87，超出正常模板同源范围，存在围标嫌疑。',
    aiGenerated: false,
  },
  {
    id: 'ev-2',
    taskId: 'cmp-1',
    type: 'similarity',
    severity: 'mid',
    docIds: ['doc-a', 'doc-c'],
    locations: [
      { docId: 'doc-a', blockIds: ['doc-a:0:1'] },
      { docId: 'doc-c', blockIds: ['doc-c:0:1'] },
    ],
    metrics: { similarity: 0.58 },
    title: '封面与格式结构相似',
    description: '封面标题与版式结构相似度 0.58，属于常见招标文件模板同源，需结合其他证据综合判断。',
    aiGenerated: false,
  },
  {
    id: 'ev-3',
    taskId: 'cmp-1',
    type: 'pricing',
    severity: 'high',
    docIds: ['doc-a', 'doc-b'],
    locations: [
      { docId: 'doc-a', blockIds: ['doc-a:2:2'] },
      { docId: 'doc-b', blockIds: ['doc-b:2:2'] },
    ],
    metrics: {},
    title: '报价尾数规律异常',
    description: '两份标书报价尾数均为「.88」（12,688.88 / 10,288.88 万元），大写金额句式完全一致，存在协商报价嫌疑。',
    aiGenerated: false,
  },
  {
    id: 'ev-4',
    taskId: 'cmp-1',
    type: 'metadata',
    severity: 'high',
    docIds: ['doc-a', 'doc-b'],
    locations: [],
    metrics: {},
    title: '文档元数据同源',
    description: '两份标书作者均为「zhang.wei」、制作工具均为 Microsoft Word，最后修改时间相差 12 分钟，疑似同一台电脑制作。',
    aiGenerated: false,
  },
  {
    id: 'ev-5',
    taskId: 'cmp-1',
    type: 'clause',
    severity: 'mid',
    docIds: ['doc-c'],
    locations: [
      { docId: 'doc-c', blockIds: ['doc-c:2:3'] },
    ],
    metrics: {},
    title: '强制性条款未实质响应：质保期',
    description: '招标文件要求质保期不少于 2 年，标书C 响应为 1 年，构成对强制性条款的未实质响应。',
    aiGenerated: true,
  },
  {
    id: 'ev-6',
    taskId: 'cmp-1',
    type: 'indicator',
    severity: 'low',
    docIds: ['doc-a', 'doc-b', 'doc-c'],
    locations: [],
    metrics: {},
    title: '工期指标离散度提示',
    description: '三家工期分别为 300 / 320 / 280 日历天，标书B 工期显著偏长，评审时可要求其澄清进度保障措施。',
    aiGenerated: true,
  },
]

// ─── 相似度矩阵（3×3，热力图用） ───

export const compareMatrix: SimilarityMatrix = {
  docIds: ['doc-a', 'doc-b', 'doc-c'],
  values: [
    [1, 0.87, 0.42],
    [0.87, 1, 0.38],
    [0.42, 0.38, 1],
  ],
}

// ─── 结构化报告 ───

export const compareReport: CompareReport = {
  taskId: 'cmp-1',
  summary: '本任务共对比 3 份标书，发现高风险证据 3 项、中风险 2 项、低风险 1 项。标书A 与标书B 技术方案大面积雷同（相似度 0.87），报价尾数规律一致且元数据同源，存在较高围标嫌疑；标书C 质保期条款未实质响应。',
  matrix: compareMatrix,
  sections: [
    {
      key: 'similarityRisk',
      title: '围标风险',
      items: [
        '标书A × 标书B 技术方案雷同（相似度 0.87，高风险）',
        '标书A × 标书B 报价尾数规律一致（高风险）',
        '标书A × 标书B 元数据同源（高风险）',
      ],
    },
    {
      key: 'clauseResponse',
      title: '条款响应',
      items: ['标书C 质保期响应 1 年，未满足「不少于 2 年」强制性要求'],
    },
    {
      key: 'indicatorCompare',
      title: '指标比选',
      items: ['报价：B（10,288.88 万）< A（12,688.88 万）< C（13,500.00 万）；工期：C 最短（280 天）'],
    },
  ],
  clauseResponses: [
    { clauseId: 'cl-1', docId: 'doc-a', status: 'compliant', reason: '具备疏浚工程专业承包一级资质，证书在有效期内。', blockIds: [] },
    { clauseId: 'cl-1', docId: 'doc-b', status: 'compliant', reason: '具备疏浚工程专业承包一级资质。', blockIds: [] },
    { clauseId: 'cl-1', docId: 'doc-c', status: 'compliant', reason: '具备疏浚工程专业承包一级资质。', blockIds: [] },
    { clauseId: 'cl-2', docId: 'doc-a', status: 'compliant', reason: '项目经理王建国，港口与航道工程一级建造师。', blockIds: [] },
    { clauseId: 'cl-2', docId: 'doc-b', status: 'partial', reason: '项目经理李海涛 2023 年取得一级建造师资格，执业年限偏短，建议评审时核实业绩。', blockIds: [] },
    { clauseId: 'cl-2', docId: 'doc-c', status: 'compliant', reason: '项目经理赵明远，港口与航道工程一级建造师。', blockIds: [] },
    { clauseId: 'cl-3', docId: 'doc-a', status: 'compliant', reason: '响应质保期 2 年。', blockIds: ['doc-a:2:3'] },
    { clauseId: 'cl-3', docId: 'doc-b', status: 'compliant', reason: '响应质保期 2 年。', blockIds: ['doc-b:2:3'] },
    { clauseId: 'cl-3', docId: 'doc-c', status: 'noncompliant', reason: '响应质保期 1 年，不满足「不少于 2 年」的强制性要求。', blockIds: ['doc-c:2:3'] },
    { clauseId: 'cl-4', docId: 'doc-a', status: 'compliant', reason: '安全生产许可证在有效期内。', blockIds: [] },
    { clauseId: 'cl-4', docId: 'doc-b', status: 'compliant', reason: '安全生产许可证在有效期内。', blockIds: [] },
    { clauseId: 'cl-4', docId: 'doc-c', status: 'compliant', reason: '安全生产许可证在有效期内。', blockIds: [] },
    { clauseId: 'cl-5', docId: 'doc-a', status: 'compliant', reason: '已按要求缴纳投标保证金。', blockIds: [] },
    { clauseId: 'cl-5', docId: 'doc-b', status: 'partial', reason: '保证金缴纳凭证日期为截止日前 1 个工作日，早于要求的 3 个工作日。', blockIds: [] },
    { clauseId: 'cl-5', docId: 'doc-c', status: 'compliant', reason: '已按要求缴纳投标保证金。', blockIds: [] },
  ],
  indicatorRows: [
    { indicator: '投标报价（万元）', values: [{ docId: 'doc-a', summary: '12,688.88' }, { docId: 'doc-b', summary: '10,288.88' }, { docId: 'doc-c', summary: '13,500.00' }] },
    { indicator: '工期（日历天）', values: [{ docId: 'doc-a', summary: '300' }, { docId: 'doc-b', summary: '320' }, { docId: 'doc-c', summary: '280' }] },
    { indicator: '资质等级', values: [{ docId: 'doc-a', summary: '疏浚专业承包一级' }, { docId: 'doc-b', summary: '疏浚专业承包一级' }, { docId: 'doc-c', summary: '疏浚专业承包一级' }] },
    { indicator: '项目经理', values: [{ docId: 'doc-a', summary: '王建国（一建·港航）' }, { docId: 'doc-b', summary: '李海涛（一建·港航，2023 取证）' }, { docId: 'doc-c', summary: '赵明远（一建·港航）' }] },
    { indicator: '质保期', values: [{ docId: 'doc-a', summary: '2 年' }, { docId: 'doc-b', summary: '2 年' }, { docId: 'doc-c', summary: '1 年（未响应）' }] },
  ],
  generatedAt: '2026-07-25 10:31',
}

// ─── 条款库模板 ───

export const compareClauseTemplates: ClauseTemplate[] = [
  { clauseId: 'tpl-1', text: '投标保证金须于截止日前 3 个工作日缴纳', category: '商务条款', mandatory: false, createdAt: '2026-06-12 11:20' },
  { clauseId: 'tpl-2', text: '履约保证金比例不超过合同价的 10%', category: '商务条款', mandatory: false, createdAt: '2026-06-12 11:21' },
  { clauseId: 'tpl-3', text: '安全文明施工措施费须单列，不得竞争性让价', category: '造价条款', mandatory: true, createdAt: '2026-06-18 16:05' },
  { clauseId: 'tpl-4', text: '须承诺开设农民工工资专用账户', category: '劳务条款', mandatory: true, createdAt: '2026-07-02 09:48' },
]

/** AI 从招标文件提取的条款草案（POST /clauses/extract 返回） */
export const compareExtractedDraft: Clause[] = [
  { clauseId: 'ext-1', source: 'extracted', text: '投标人须具备疏浚工程专业承包一级资质', mandatory: true, category: '资质要求' },
  { clauseId: 'ext-2', source: 'extracted', text: '项目经理须具备港口与航道工程一级建造师资格', mandatory: true, category: '人员要求' },
  { clauseId: 'ext-3', source: 'extracted', text: '质保期不少于 2 年', mandatory: true, category: '商务条款' },
  { clauseId: 'ext-4', source: 'extracted', text: '须提供安全生产许可证且在有效期内', mandatory: true, category: '资质要求' },
]

// ─── IR（页面 595×842 为真实尺寸；bbox 为 0~1 归一化值 = 演示 PDF 文本行坐标 ÷ 页面尺寸，v2 契约；blockId 采用 block_uid 风格） ───

function makeIr(overrides: {
  docId: string
  fileName: string
  author: string | null
  creatorTool: string | null
  modifiedAt: string | null
  blocks: IrDocument['blocks']
  outline: IrDocument['outline']
}): IrDocument {
  return {
    // 内部适配 IR 版本（v2 映射形态；1.0 为已废止的 ir.json 交付契约）
    schemaVersion: '2.0',
    docId: overrides.docId,
    meta: {
      fileName: overrides.fileName,
      pageCount: 3,
      author: overrides.author,
      creatorTool: overrides.creatorTool,
      createdAt: '2026-07-18T09:12:00Z',
      modifiedAt: overrides.modifiedAt,
    },
    pages: [
      { pageIdx: 0, width: 595, height: 842 },
      { pageIdx: 1, width: 595, height: 842 },
      { pageIdx: 2, width: 595, height: 842 },
    ],
    outline: overrides.outline,
    blocks: overrides.blocks,
  }
}

const SHARED_TECH_1 = '3.1 施工总体部署：本工程投入耙吸式挖泥船两艘，采用分段分条施工法'
const SHARED_TECH_2 = '3.2 疏浚工艺流程：定位→下耙→拖航→溢流→起耙→吹填上岸'
const SHARED_TECH_3 = '3.3 质量控制：执行 JTS 257《疏浚工程质量检验标准》全程自检'

const irA = makeIr({
  docId: 'doc-a',
  fileName: '中港疏浚有限公司投标文件.pdf',
  author: 'zhang.wei',
  creatorTool: 'Microsoft Word',
  modifiedAt: '2026-07-20T21:40:00Z',
  outline: [
    { title: '第三章 技术方案', level: 1, blockId: 'doc-a:1:1', children: [] },
    { title: '第五章 商务报价', level: 1, blockId: 'doc-a:2:1', children: [] },
  ],
  blocks: [
    { blockId: 'doc-a:0:1', pageIdx: 0, bbox: [0.1008, 0.0855, 0.5546, 0.1188], type: 'title', text: '智慧航道疏浚工程投标文件', textLevel: 1, source: 'native', confidence: 1 },
    { blockId: 'doc-a:0:2', pageIdx: 0, bbox: [0.1008, 0.1663, 0.3697, 0.1948], type: 'para', text: '投标人：中港疏浚有限公司', textLevel: 0, source: 'native', confidence: 1 },
    { blockId: 'doc-a:0:3', pageIdx: 0, bbox: [0.1008, 0.2043, 0.3866, 0.2328], type: 'para', text: '日期：2026 年 7 月 20 日', textLevel: 0, source: 'native', confidence: 1 },
    { blockId: 'doc-a:1:1', pageIdx: 1, bbox: [0.1008, 0.0713, 0.4874, 0.1045], type: 'title', text: '第三章 技术方案', textLevel: 1, source: 'native', confidence: 1 },
    { blockId: 'doc-a:1:2', pageIdx: 1, bbox: [0.1008, 0.1306, 0.8992, 0.1615], type: 'para', text: SHARED_TECH_1, textLevel: 0, source: 'native', confidence: 1 },
    { blockId: 'doc-a:1:3', pageIdx: 1, bbox: [0.1008, 0.1734, 0.8992, 0.2043], type: 'para', text: SHARED_TECH_2, textLevel: 0, source: 'native', confidence: 1 },
    { blockId: 'doc-a:1:4', pageIdx: 1, bbox: [0.1008, 0.2162, 0.8992, 0.247], type: 'para', text: SHARED_TECH_3, textLevel: 0, source: 'native', confidence: 1 },
    { blockId: 'doc-a:2:1', pageIdx: 2, bbox: [0.1008, 0.0713, 0.4874, 0.1045], type: 'title', text: '第五章 商务报价', textLevel: 1, source: 'native', confidence: 1 },
    { blockId: 'doc-a:2:2', pageIdx: 2, bbox: [0.1008, 0.1306, 0.5378, 0.1615], type: 'para', text: '投标总价：人民币 12,688.88 万元', textLevel: 0, source: 'native', confidence: 1 },
    { blockId: 'doc-a:2:3', pageIdx: 2, bbox: [0.1008, 0.1734, 0.5042, 0.2043], type: 'para', text: '工期：300 日历天，质保期 2 年', textLevel: 0, source: 'native', confidence: 1 },
  ],
})

const irB = makeIr({
  docId: 'doc-b',
  fileName: '长江航道工程局投标文件.pdf',
  author: 'zhang.wei',
  creatorTool: 'Microsoft Word',
  modifiedAt: '2026-07-20T21:52:00Z',
  outline: [
    { title: '第三章 技术方案', level: 1, blockId: 'doc-b:1:1', children: [] },
    { title: '第五章 商务报价', level: 1, blockId: 'doc-b:2:1', children: [] },
  ],
  blocks: [
    { blockId: 'doc-b:0:1', pageIdx: 0, bbox: [0.1008, 0.0855, 0.5546, 0.1188], type: 'title', text: '智慧航道疏浚工程投标文件', textLevel: 1, source: 'ocr', confidence: 0.62 },
    { blockId: 'doc-b:0:2', pageIdx: 0, bbox: [0.1008, 0.1663, 0.3697, 0.1948], type: 'para', text: '投标人：长江航道工程局', textLevel: 0, source: 'ocr', confidence: 0.58 },
    { blockId: 'doc-b:0:3', pageIdx: 0, bbox: [0.1008, 0.2043, 0.3866, 0.2328], type: 'para', text: '日期：2026 年 7 月 21 日', textLevel: 0, source: 'ocr', confidence: 0.6 },
    { blockId: 'doc-b:1:1', pageIdx: 1, bbox: [0.1008, 0.0713, 0.4874, 0.1045], type: 'title', text: '第三章 技术方案', textLevel: 1, source: 'ocr', confidence: 0.66 },
    { blockId: 'doc-b:1:2', pageIdx: 1, bbox: [0.1008, 0.1306, 0.8992, 0.1615], type: 'para', text: SHARED_TECH_1, textLevel: 0, source: 'ocr', confidence: 0.55 },
    { blockId: 'doc-b:1:3', pageIdx: 1, bbox: [0.1008, 0.1734, 0.8992, 0.2043], type: 'para', text: SHARED_TECH_2, textLevel: 0, source: 'ocr', confidence: 0.57 },
    { blockId: 'doc-b:1:4', pageIdx: 1, bbox: [0.1008, 0.2162, 0.8992, 0.247], type: 'para', text: SHARED_TECH_3, textLevel: 0, source: 'ocr', confidence: 0.6 },
    { blockId: 'doc-b:2:1', pageIdx: 2, bbox: [0.1008, 0.0713, 0.4874, 0.1045], type: 'title', text: '第五章 商务报价', textLevel: 1, source: 'ocr', confidence: 0.64 },
    { blockId: 'doc-b:2:2', pageIdx: 2, bbox: [0.1008, 0.1306, 0.5378, 0.1615], type: 'para', text: '投标总价：人民币 10,288.88 万元', textLevel: 0, source: 'ocr', confidence: 0.61 },
    { blockId: 'doc-b:2:3', pageIdx: 2, bbox: [0.1008, 0.1734, 0.5042, 0.2043], type: 'para', text: '工期：320 日历天，质保期 2 年', textLevel: 0, source: 'ocr', confidence: 0.63 },
  ],
})

const irC = makeIr({
  docId: 'doc-c',
  fileName: '海工建设集团投标文件.pdf',
  author: 'li.na',
  creatorTool: 'WPS Office',
  modifiedAt: '2026-07-21T14:05:00Z',
  outline: [
    { title: '第三章 施工组织设计', level: 1, blockId: 'doc-c:1:1', children: [] },
    { title: '第五章 商务报价', level: 1, blockId: 'doc-c:2:1', children: [] },
  ],
  blocks: [
    { blockId: 'doc-c:0:1', pageIdx: 0, bbox: [0.1008, 0.0855, 0.7227, 0.1188], type: 'title', text: '智慧航道疏浚工程投标文件（技术标）', textLevel: 1, source: 'native', confidence: 1 },
    { blockId: 'doc-c:0:2', pageIdx: 0, bbox: [0.1008, 0.1663, 0.3361, 0.1948], type: 'para', text: '投标人：海工建设集团', textLevel: 0, source: 'native', confidence: 1 },
    { blockId: 'doc-c:0:3', pageIdx: 0, bbox: [0.1008, 0.2043, 0.3866, 0.2328], type: 'para', text: '日期：2026 年 7 月 21 日', textLevel: 0, source: 'native', confidence: 1 },
    { blockId: 'doc-c:1:1', pageIdx: 1, bbox: [0.1008, 0.0713, 0.5546, 0.1045], type: 'title', text: '第三章 施工组织设计', textLevel: 1, source: 'native', confidence: 1 },
    { blockId: 'doc-c:1:2', pageIdx: 1, bbox: [0.1008, 0.1306, 0.8992, 0.1615], type: 'para', text: '3.1 工艺方案：采用绞吸式挖泥船加接力泵站，管线吹填上岸', textLevel: 0, source: 'native', confidence: 1 },
    { blockId: 'doc-c:1:3', pageIdx: 1, bbox: [0.1008, 0.1734, 0.8992, 0.2043], type: 'para', text: '3.2 进度安排：总工期 280 日历天，分三个施工段流水作业', textLevel: 0, source: 'native', confidence: 1 },
    { blockId: 'doc-c:2:1', pageIdx: 2, bbox: [0.1008, 0.0713, 0.4874, 0.1045], type: 'title', text: '第五章 商务报价', textLevel: 1, source: 'native', confidence: 1 },
    { blockId: 'doc-c:2:2', pageIdx: 2, bbox: [0.1008, 0.1306, 0.5378, 0.1615], type: 'para', text: '投标总价：人民币 13,500.00 万元', textLevel: 0, source: 'native', confidence: 1 },
    { blockId: 'doc-c:2:3', pageIdx: 2, bbox: [0.1008, 0.1734, 0.5042, 0.2043], type: 'para', text: '工期：280 日历天，质保期 1 年', textLevel: 0, source: 'native', confidence: 1 },
  ],
})

/** docId → IR（mock 路由按 docId 查取，新任务的文档复用同一份 PDF/IR 做演示） */
export const compareIrMap: Record<string, IrDocument> = {
  'doc-a': irA,
  'doc-b': irB,
  'doc-c': irC,
}

/** 新任务上传时的演示文档池（轮询分配 PDF + IR 模板） */
export const compareDocPool = [
  { fileUrl: '/mock/compare/bid-a.pdf', irKey: 'doc-a', pageCount: 3 },
  { fileUrl: '/mock/compare/bid-b.pdf', irKey: 'doc-b', pageCount: 3 },
  { fileUrl: '/mock/compare/bid-c.pdf', irKey: 'doc-c', pageCount: 3 },
]
```

- [ ] **Step 2: 验证** — `pnpm run typecheck` 通过。无界面走查。
- [ ] **Step 3: （可选）** `git add -A && git commit -m "feat(shared): add compare mock data"`

---

## Task 4 【P1】API 模块

**Files:**
- Create: `user-web/src/api/modules/compare.ts`

- [ ] **Step 1: 创建 `user-web/src/api/modules/compare.ts`，内容如下（完整文件）**

```ts
import request from '@/api/request'
import { urls } from '@shared/core/api'
import type {
  Clause,
  ClauseTemplate,
  CompareDocument,
  CompareReport,
  CompareTask,
  CompareTaskDetail,
  Evidence,
  ExportJob,
  IrDocument,
  PagedResult,
  SimilarityMatrix,
} from '@/types'

/** 将 urls 模板中的 :param 占位替换为实际值（与 admin-web dubbing 模块同一写法） */
function fill(tpl: string, params: Record<string, string>): string {
  return Object.entries(params).reduce((u, [k, v]) => u.replace(`:${k}`, v), tpl)
}

export function getCompareTasks(params?: { page?: number, pageSize?: number }): Promise<PagedResult<CompareTask>> {
  return request.get<PagedResult<CompareTask>>(urls.compareTasks, { params })
}

export function createCompareTask(data: { name: string }): Promise<CompareTask> {
  return request.post<CompareTask>(urls.compareTasks, data)
}

/** 上传文档（标书/招标文件，role 区分；spec §6 POST /tasks/{id}/documents） */
export function uploadCompareDocument(taskId: string, file: File, role: 'bid' | 'tender'): Promise<CompareDocument> {
  const formData = new FormData()
  formData.append('file', file)
  formData.append('role', role)
  return request.post<CompareDocument>(fill(urls.compareTaskDocuments, { id: taskId }), formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
}

export function getCompareTask(id: string): Promise<CompareTaskDetail> {
  return request.get<CompareTaskDetail>(fill(urls.compareTask, { id }))
}

export function deleteCompareTask(id: string): Promise<void> {
  return request.delete<void>(fill(urls.compareTask, { id }))
}

export function getCompareEvidences(
  taskId: string,
  filters?: { type?: Evidence['type'], severity?: Evidence['severity'] },
): Promise<Evidence[]> {
  return request.get<Evidence[]>(fill(urls.compareTaskEvidences, { id: taskId }), { params: filters })
}

export function getCompareMatrix(taskId: string): Promise<SimilarityMatrix> {
  return request.get<SimilarityMatrix>(fill(urls.compareTaskMatrix, { id: taskId }))
}

/** 某文档的 IR（左右对比视图画 bbox 用） */
export function getCompareIr(taskId: string, docId: string): Promise<IrDocument> {
  return request.get<IrDocument>(fill(urls.compareTaskIr, { id: taskId, docId }))
}

export function getCompareReport(taskId: string): Promise<CompareReport> {
  return request.get<CompareReport>(fill(urls.compareTaskReport, { id: taskId }))
}

/** 触发从招标文件提取条款草案 */
export function extractCompareClauses(taskId: string): Promise<Clause[]> {
  return request.post<Clause[]>(fill(urls.compareTaskClausesExtract, { id: taskId }))
}

/** 确认后的条款清单（锁定快照，锁定后任务进入查重） */
export function lockCompareClauses(taskId: string, clauses: Clause[]): Promise<CompareTask> {
  return request.put<CompareTask>(fill(urls.compareTaskClauses, { id: taskId }), { clauses })
}

export function getClauseTemplates(): Promise<PagedResult<ClauseTemplate>> {
  return request.get<PagedResult<ClauseTemplate>>(urls.compareClauseTemplates)
}

export function createClauseTemplate(data: { text: string, category: string, mandatory: boolean }): Promise<ClauseTemplate> {
  return request.post<ClauseTemplate>(urls.compareClauseTemplates, data)
}

/** 生成导出文件（异步，返回任务句柄；spec §6.2） */
export function exportCompareReport(taskId: string, format: 'pdf' | 'word'): Promise<ExportJob> {
  return request.post<ExportJob>(fill(urls.compareTaskExport, { id: taskId }), { format })
}

/** 轮询导出句柄状态（前端补充端点，见计划「假设」） */
export function getCompareExportStatus(taskId: string, exportId: string): Promise<ExportJob> {
  return request.get<ExportJob>(fill(urls.compareTaskExportStatus, { id: taskId, exportId }))
}
```

- [ ] **Step 2: 验证** — `pnpm run typecheck` 通过。无界面走查。
- [ ] **Step 3: （可选）** `git add -A && git commit -m "feat(user-web): add compare api module"`

---

## Task 5 【P1】mock 路由与注册

**Files:**
- Create: `user-web/src/mock/routes/compare.ts`
- Modify: `user-web/src/utils/constants.ts`
- Modify: `user-web/src/mock/index.ts`

mock 内置任务状态机：新任务 `parsing`（约 2.4s）→ `parsed`；无招标文件自动进 `comparing`，有招标文件停住等 `PUT clauses` 锁定；`comparing`（约 2.8s）完成后证据可用并进 `analyzing`（约 2.8s）→ `done`。新任务的证据/矩阵/IR 复用演示数据（docId 重写为新任务文档 id）。

- [ ] **Step 1: 创建 `user-web/src/mock/routes/compare.ts`，内容如下（完整文件）**

```ts
import type MockAdapter from 'axios-mock-adapter'
import {
  compareTaskDetails,
  compareEvidences,
  compareMatrix,
  compareReport,
  compareClauseTemplates,
  compareExtractedDraft,
  compareIrMap,
  compareDocPool,
} from '@shared/mock/data/compare'
import type {
  Clause,
  CompareDocument,
  CompareTask,
  CompareTaskDetail,
  Evidence,
  ExportJob,
  IrDocument,
} from '@/types'

// ─── 内存存储（克隆演示数据，页面操作不影响源数据） ───

const tasks: CompareTaskDetail[] = structuredClone(compareTaskDetails)
const evidencesByTask: Record<string, Evidence[]> = { 'cmp-1': structuredClone(compareEvidences) }
// cmp-2（analyzing）：查重已完成、AI 补齐中，证据先行可见（rewriteEvidences 为函数声明，提升可用）
evidencesByTask['cmp-2'] = rewriteEvidences(tasks.find((t) => t.id === 'cmp-2')!)
const exportJobs = new Map<string, ExportJob>()
const clauseTemplates = structuredClone(compareClauseTemplates)

let nextTaskId = 100
let nextDocId = 100
let nextExportId = 1
let nextTemplateId = 100

function findTask(id: string): CompareTaskDetail | undefined {
  return tasks.find((t) => t.id === id)
}

function setStage(task: CompareTaskDetail, stage: CompareTaskDetail['progress'][number]['stage'], patch: Partial<CompareTaskDetail['progress'][number]>): void {
  const s = task.progress.find((p) => p.stage === stage)
  if (s) Object.assign(s, patch)
}

/** 新任务的证据：复用演示证据，locations 的 docId 重写为新任务的标书 id */
function rewriteEvidences(task: CompareTaskDetail): Evidence[] {
  const bids = task.documents.filter((d) => d.role === 'bid')
  if (bids.length < 2) return []
  return structuredClone(compareEvidences).map((ev) => {
    const locations = ev.locations.map((loc, i) => ({ docId: bids[i % bids.length].id, blockIds: loc.blockIds }))
    const docIds = [...new Set(locations.map((l) => l.docId))]
    return { ...ev, taskId: task.id, docIds: docIds.length > 0 ? docIds : bids.slice(0, 2).map((d) => d.id), locations }
  })
}

function driveAi(task: CompareTaskDetail): void {
  task.status = 'analyzing'
  setStage(task, 'aiAnalysis', { status: 'process' })
  let pct = 0
  const timer = setInterval(() => {
    pct += 25
    setStage(task, 'aiAnalysis', { percent: Math.min(pct, 100) })
    if (pct >= 100) {
      clearInterval(timer)
      setStage(task, 'aiAnalysis', { status: 'finish', percent: 100 })
      task.status = 'done'
    }
  }, 700)
}

function startComparing(task: CompareTaskDetail): void {
  task.status = 'comparing'
  setStage(task, 'comparing', { status: 'process' })
  let pct = 0
  const timer = setInterval(() => {
    pct += 25
    setStage(task, 'comparing', { percent: Math.min(pct, 100) })
    if (pct >= 100) {
      clearInterval(timer)
      setStage(task, 'comparing', { status: 'finish', percent: 100 })
      setStage(task, 'clauseCheck', { status: 'finish', percent: 100 })
      // 查重完成：证据落库，前端「先到先展示」（spec §5.4）
      evidencesByTask[task.id] = rewriteEvidences(task)
      task.highRiskCount = evidencesByTask[task.id].filter((e) => e.severity === 'high').length
      driveAi(task)
    }
  }, 700)
}

/** 任务状态机驱动：解析完成后按有无招标文件分流（spec §3.2 条款必须先锁定） */
function driveParsing(task: CompareTaskDetail): void {
  let pct = 0
  const timer = setInterval(() => {
    pct += 40
    setStage(task, 'parsing', { percent: Math.min(pct, 100) })
    if (pct >= 100) {
      clearInterval(timer)
      setStage(task, 'parsing', { status: 'finish', percent: 100 })
      task.status = 'parsed'
      for (const doc of task.documents) doc.parseStatus = 'done'
      if (!task.tenderDocId) startComparing(task)
      // 有招标文件：停在 parsed，等待 PUT /clauses 锁定后进入查重
    }
  }, 600)
}

function toListItem(task: CompareTaskDetail): CompareTask {
  const { documents: _documents, ...rest } = task
  return { ...rest, highRiskCount: (evidencesByTask[task.id] ?? []).filter((e) => e.severity === 'high').length || task.highRiskCount || 0 }
}

function matchId(url: string | undefined, re: RegExp): string {
  return url?.match(re)?.[1] ?? ''
}

export function registerCompareMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  // 注意注册顺序：具体子路径先于 /tasks/:id 泛匹配

  // 条款库
  mock.onGet('/api/compare/clause-templates').reply(wrap(() => ({ items: clauseTemplates, totalCount: clauseTemplates.length })))
  mock.onPost('/api/compare/clause-templates').reply((config) => {
    const body = JSON.parse(config.data) as { text: string, category: string, mandatory: boolean }
    const tpl = { clauseId: `tpl-${nextTemplateId++}`, ...body, createdAt: new Date().toISOString().slice(0, 16).replace('T', ' ') }
    clauseTemplates.push(tpl)
    return [200, tpl]
  })

  // IR
  mock.onGet(/\/api\/compare\/tasks\/([^/]+)\/ir\/([^/]+)$/).reply((config) => {
    const docId = matchId(config.url, /\/ir\/([^/]+)$/)
    const ir = compareIrMap[docId]
    // 新任务的文档不在演示池：回退到 doc-a 的 IR 并改写 docId（演示足够）
    const fallback: IrDocument | undefined = ir ?? (compareIrMap['doc-a'] ? { ...compareIrMap['doc-a'], docId } : undefined)
    return fallback ? [200, fallback] : [404, { message: 'IR not found' }]
  })

  // 证据
  mock.onGet(/\/api\/compare\/tasks\/([^/]+)\/evidences$/).reply((config) => {
    const taskId = matchId(config.url, /\/tasks\/([^/]+)\/evidences$/)
    let list = evidencesByTask[taskId] ?? []
    const type = config.params?.type as Evidence['type'] | undefined
    const severity = config.params?.severity as Evidence['severity'] | undefined
    if (type) list = list.filter((e) => e.type === type)
    if (severity) list = list.filter((e) => e.severity === severity)
    return [200, list]
  })

  // 矩阵
  mock.onGet(/\/api\/compare\/tasks\/([^/]+)\/matrix$/).reply((config) => {
    const taskId = matchId(config.url, /\/tasks\/([^/]+)\/matrix$/)
    const task = findTask(taskId)
    if (!task) return [404, { message: 'Task not found' }]
    const bids = task.documents.filter((d) => d.role === 'bid')
    const n = Math.min(bids.length, compareMatrix.values.length)
    return [200, {
      docIds: bids.slice(0, n).map((d) => d.id),
      values: compareMatrix.values.slice(0, n).map((row) => row.slice(0, n)),
    }]
  })

  // 报告
  mock.onGet(/\/api\/compare\/tasks\/([^/]+)\/report$/).reply((config) => {
    const taskId = matchId(config.url, /\/tasks\/([^/]+)\/report$/)
    const task = findTask(taskId)
    if (!task) return [404, { message: 'Task not found' }]
    if (task.id === 'cmp-1') return [200, compareReport]
    const bids = task.documents.filter((d) => d.role === 'bid')
    // 非演示任务：条款/指标数据与 cmp-1 文档 id 绑定，置空避免串数据（条款矩阵/指标表走 EmptyState）
    return [200, {
      ...compareReport,
      taskId: task.id,
      sections: [],
      clauseResponses: [],
      indicatorRows: [],
      matrix: {
        docIds: bids.slice(0, 3).map((d) => d.id),
        values: compareMatrix.values.slice(0, bids.length).map((row) => row.slice(0, bids.length)),
      },
    }]
  })

  // 导出（异步句柄 + 状态轮询）
  mock.onPost(/\/api\/compare\/tasks\/([^/]+)\/export$/).reply((config) => {
    const taskId = matchId(config.url, /\/tasks\/([^/]+)\/export$/)
    const body = JSON.parse(config.data) as { format: 'pdf' | 'word' }
    const job: ExportJob = { exportId: `exp-${nextExportId++}`, format: body.format, status: 'processing' }
    exportJobs.set(`${taskId}:${job.exportId}`, job)
    setTimeout(() => {
      job.status = 'done'
      job.downloadUrl = '/mock/compare/report-demo.pdf'
    }, 2500)
    return [200, job]
  })
  mock.onGet(/\/api\/compare\/tasks\/([^/]+)\/export\/([^/]+)$/).reply((config) => {
    const taskId = matchId(config.url, /\/tasks\/([^/]+)\/export\//)
    const exportId = matchId(config.url, /\/export\/([^/]+)$/)
    const job = exportJobs.get(`${taskId}:${exportId}`)
    return job ? [200, job] : [404, { message: 'Export job not found' }]
  })

  // 条款提取 / 锁定
  mock.onPost(/\/api\/compare\/tasks\/([^/]+)\/clauses\/extract$/).reply(wrap(() => structuredClone(compareExtractedDraft)))
  mock.onPut(/\/api\/compare\/tasks\/([^/]+)\/clauses$/).reply((config) => {
    const taskId = matchId(config.url, /\/tasks\/([^/]+)\/clauses$/)
    const task = findTask(taskId)
    if (!task) return [404, { message: 'Task not found' }]
    const body = JSON.parse(config.data) as { clauses: Clause[] }
    task.clauseSnapshot = body.clauses
    if (task.status === 'parsed') startComparing(task)
    return [200, toListItem(task)]
  })

  // 上传文档
  mock.onPost(/\/api\/compare\/tasks\/([^/]+)\/documents$/).reply((config) => {
    const taskId = matchId(config.url, /\/tasks\/([^/]+)\/documents$/)
    const task = findTask(taskId)
    if (!task) return [404, { message: 'Task not found' }]
    const fd = config.data as FormData
    const role = (fd.get('role') as 'bid' | 'tender') || 'bid'
    const file = fd.get('file') as File | null
    const bidCount = task.documents.filter((d) => d.role === 'bid').length
    const poolItem = compareDocPool[bidCount % compareDocPool.length]
    const doc: CompareDocument = {
      id: `cmp${nextTaskId}-d${nextDocId++}`,
      taskId: task.id,
      role,
      fileName: file?.name || '未命名文件.pdf',
      shortName: role === 'tender' ? '招标文件' : `标书${'ABCDE'[bidCount]}`,
      pageCount: poolItem.pageCount,
      parseStatus: 'parsing',
      ocrLowConfidenceRatio: 0.03,
      fileUrl: poolItem.fileUrl,
    }
    task.documents.push(doc)
    if (role === 'tender') task.tenderDocId = doc.id
    else task.docIds.push(doc.id)
    return [200, doc]
  })

  // 任务详情
  mock.onGet(/\/api\/compare\/tasks\/([^/]+)$/).reply((config) => {
    const task = findTask(matchId(config.url, /\/tasks\/([^/]+)$/))
    return task ? [200, task] : [404, { message: 'Task not found' }]
  })

  // 删除任务
  mock.onDelete(/\/api\/compare\/tasks\/([^/]+)$/).reply((config) => {
    const id = matchId(config.url, /\/tasks\/([^/]+)$/)
    const idx = tasks.findIndex((t) => t.id === id)
    if (idx >= 0) tasks.splice(idx, 1)
    return [204, undefined]
  })

  // 任务列表 / 创建
  mock.onGet('/api/compare/tasks').reply(wrap(() => {
    const items = [...tasks].sort((a, b) => b.createdAt.localeCompare(a.createdAt)).map(toListItem)
    return { items, totalCount: items.length }
  }))
  mock.onPost('/api/compare/tasks').reply((config) => {
    const body = JSON.parse(config.data) as { name: string }
    const task: CompareTaskDetail = {
      id: `cmp-${nextTaskId++}`,
      name: body.name,
      status: 'parsing',
      docIds: [],
      clauseSnapshot: [],
      progress: [
        { stage: 'parsing', label: '文档解析', status: 'process', percent: 0 },
        { stage: 'comparing', label: '两两查重', status: 'wait', percent: 0 },
        { stage: 'clauseCheck', label: '条款校验', status: 'wait', percent: 0 },
        { stage: 'aiAnalysis', label: 'AI 分析', status: 'wait', percent: 0 },
      ],
      createdAt: new Date().toISOString().slice(0, 16).replace('T', ' '),
      highRiskCount: 0,
      documents: [],
    }
    tasks.unshift(task)
    driveParsing(task)
    return [200, toListItem(task)]
  })
}
```

- [ ] **Step 2: 修改 `user-web/src/utils/constants.ts`，`MOCK_MODULES` 中 `dubbing: true,` 之后追加一行**

```ts
  compare: true,
```

- [ ] **Step 3: 修改 `user-web/src/mock/index.ts`**

import 区追加（放在 `registerDubbingMock` import 之后）：

```ts
import { registerCompareMock } from './routes/compare'
```

`modules` 数组中 `{ key: 'dubbing', register: registerDubbingMock },` 之后追加：

```ts
    { key: 'compare', register: registerCompareMock },
```

- [ ] **Step 4: 验证** — `pnpm run typecheck` 通过。手动走查：`pnpm dev` 启动后打开浏览器 DevTools Console 执行 `fetch('/api/compare/tasks').then(r=>r.json()).then(console.log)`，应返回含 `cmp-1/cmp-2/cmp-3` 的 `{ items, totalCount }`；执行 `fetch('/api/compare/tasks/cmp-1/evidences').then(r=>r.json()).then(console.log)` 应返回 6 条证据。
- [ ] **Step 5: （可选）** `git add -A && git commit -m "feat(user-web): add compare mock routes"`

---

## Task 6 【P1】路由确认 + 页面常量 + 任务列表页

**Files:**
- Confirm unchanged: `user-web/src/router/manifests.ts`（`ai-bid-compare` 已占位，组件路径 `@/views/ai-bid/compare/index.vue` 不变，本模块不新增路由记录，视图切换全在组件内状态机完成）
- Create: `user-web/src/views/ai-bid/compare/constants.ts`
- Create: `user-web/src/views/ai-bid/compare/components/TaskList.vue`
- Modify: `user-web/src/views/ai-bid/compare/index.vue`（替换占位页）

- [ ] **Step 1: 确认 `user-web/src/router/manifests.ts` 中 `ai-bid-compare` 条目不变（route `/compare`、component `@/views/ai-bid/compare/index.vue`），无需修改。**

- [ ] **Step 2: 创建 `user-web/src/views/ai-bid/compare/constants.ts`，内容如下（完整文件）**

```ts
import type { CompareTaskStatus, Evidence } from '@/types'

/** compare/index.vue 的视图状态机（组件内切换，不加路由） */
export type CompareView = 'list' | 'create' | 'clauses' | 'progress' | 'result' | 'diff'

export const EVIDENCE_TYPE_LABELS: Record<Evidence['type'], string> = {
  similarity: '雷同',
  pricing: '报价',
  metadata: '元数据',
  clause: '条款',
  indicator: '指标',
}

export const SEVERITY_LABELS: Record<Evidence['severity'], string> = {
  high: '高风险',
  mid: '中风险',
  low: '低风险',
}

/** 严重度色（AGENTS.md §2.1 配色速查表：高 #EF4444 / 中 #F59E0B / 低 #3B82F6） */
export const SEVERITY_COLORS: Record<Evidence['severity'], string> = {
  high: '#EF4444',
  mid: '#F59E0B',
  low: '#3B82F6',
}

export const TASK_STATUS_LABELS: Record<CompareTaskStatus, string> = {
  parsing: '解析中',
  parsed: '待确认条款',
  comparing: '查重中',
  analyzing: 'AI 分析中',
  done: '已完成',
  failed: '失败',
  partial: '部分完成',
}

/** 状态色（AGENTS.md §2.1：进行中 blue / 成功 green / 失败 red） */
export const TASK_STATUS_COLORS: Record<CompareTaskStatus, string> = {
  parsing: 'blue',
  parsed: 'blue',
  comparing: 'blue',
  analyzing: 'blue',
  done: 'green',
  failed: 'red',
  partial: 'orange',
}
```

- [ ] **Step 3: 创建 `user-web/src/views/ai-bid/compare/components/TaskList.vue`，内容如下（完整文件）**

```vue
<template>
  <div class="task-list">
    <PageHeader title="比标任务" description="上传 2~5 份标书，自动完成查重、条款校验与指标比选">
      <template #extra>
        <a-button type="primary" @click="emit('create')">
          <PlusOutlined /> 创建任务
        </a-button>
      </template>
    </PageHeader>

    <SectionCard nopad>
      <a-skeleton v-if="loading" :paragraph="{ rows: 5 }" class="task-list__skeleton" />

      <EmptyState v-else-if="tasks.length === 0" type="no-data" title="暂无比标任务" description="创建任务并上传标书，开始对比分析">
        <template #action>
          <a-button type="primary" @click="emit('create')">创建任务</a-button>
        </template>
      </EmptyState>

      <a-table
        v-else
        size="small"
        :data-source="tasks"
        :columns="columns"
        :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 条` }"
        row-key="id"
        :scroll="{ x: 900 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'docCount'">{{ record.docIds.length }} 份</template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="TASK_STATUS_COLORS[record.status as CompareTaskStatus]">
              {{ TASK_STATUS_LABELS[record.status as CompareTaskStatus] }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'highRiskCount'">
            <span :class="{ 'task-list__high-risk': (record.highRiskCount ?? 0) > 0 }">
              {{ record.highRiskCount ?? 0 }}
            </span>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-button type="link" size="small" @click="emit('view', record)">查看</a-button>
            <a-popconfirm title="确认删除该任务？" @confirm="emit('remove', record)">
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import EmptyState from '@shared/web/components/EmptyState.vue'
import type { CompareTask, CompareTaskStatus } from '@/types'
import { TASK_STATUS_COLORS, TASK_STATUS_LABELS } from '../constants'

defineProps<{ tasks: CompareTask[], loading: boolean }>()

const emit = defineEmits<{
  create: []
  view: [task: CompareTask]
  remove: [task: CompareTask]
}>()

const columns = [
  { title: '任务名', dataIndex: 'name', key: 'name' },
  { title: '标书份数', key: 'docCount', width: 100 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 120 },
  { title: '高风险数', dataIndex: 'highRiskCount', key: 'highRiskCount', width: 100 },
  { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', width: 180 },
  { title: '操作', key: 'action', width: 180 },
]
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.task-list__skeleton { padding: @spacing-xl; }

.task-list__high-risk {
  color: @danger;
  font-weight: @font-weight-semibold;
}
</style>
```

- [ ] **Step 4: 替换 `user-web/src/views/ai-bid/compare/index.vue` 占位页为以下内容（完整文件；本 Task 仅启用 `list` 视图，后续 Task 逐个追加视图分支与 handler）**

```vue
<template>
  <div class="compare-page">
    <TaskList
      v-if="view === 'list'"
      :tasks="tasks"
      :loading="listLoading"
      @create="view = 'create'"
      @view="openTask"
      @remove="handleRemove"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { message } from 'ant-design-vue'
import TaskList from './components/TaskList.vue'
import {
  getCompareTasks,
  getCompareTask,
  deleteCompareTask,
} from '@/api/modules/compare'
import type { CompareTask, CompareTaskDetail } from '@/types'
import type { CompareView } from './constants'

/** 视图状态机：list → create → (clauses) → progress → result → diff，均在本组件内切换 */
const view = ref<CompareView>('list')

// ─── 任务列表 ───
const tasks = ref<CompareTask[]>([])
const listLoading = ref(false)

// ─── 当前任务（progress/result/diff 视图共享） ───
const currentTask = ref<CompareTaskDetail | null>(null)

let pollTimer: ReturnType<typeof setInterval> | undefined

function stopPolling(): void {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = undefined
  }
}

async function loadTasks(): Promise<void> {
  listLoading.value = true
  try {
    const res = await getCompareTasks()
    tasks.value = res.items
  } catch {
    message.error('加载比标任务失败')
  } finally {
    listLoading.value = false
  }
}

async function openTask(task: CompareTask): Promise<void> {
  try {
    currentTask.value = await getCompareTask(task.id)
  } catch {
    message.error('加载任务详情失败')
  }
  // Task 8/9 将按状态进入 progress / result 视图
}

async function handleRemove(task: CompareTask): Promise<void> {
  try {
    await deleteCompareTask(task.id)
    tasks.value = tasks.value.filter((t) => t.id !== task.id)
    message.success('已删除')
  } catch {
    message.error('删除失败')
  }
}

onMounted(loadTasks)
onUnmounted(stopPolling)
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.compare-page {
  min-height: 400px;
}
</style>
```

- [ ] **Step 5: 验证** — `pnpm run typecheck` 通过。手动走查：`pnpm dev` → 登录后进「AI投标 → 比标」，看到 3 条任务（智慧航道疏浚工程比标=已完成绿色 tag、港区护岸修复工程比标=AI 分析中蓝色、锚地疏浚维护比标=部分完成橙色）；「高风险数」列 cmp-1 显示红色加粗 3；点「删除」popconfirm 确认后行消失；「创建任务」按钮可点（视图切换在 Task 7 生效，本 Task 点击后空白属预期，走查时刷新回列表）。
- [ ] **Step 6: （可选）** `git add -A && git commit -m "feat(user-web): compare task list page"`

---

## Task 7 【P1】创建任务（上传）

**Files:**
- Create: `user-web/src/views/ai-bid/compare/components/TaskCreate.vue`
- Modify: `user-web/src/views/ai-bid/compare/index.vue`（追加 `create` 视图分支与 `handleCreate`）

交互参考 `VoiceRegisterUploadTab` 的拖拽上传语言；标书 2~5 份（PDF/Word）+ 可选招标文件 1 份。

- [ ] **Step 1: 创建 `user-web/src/views/ai-bid/compare/components/TaskCreate.vue`，内容如下（完整文件）**

```vue
<template>
  <div class="task-create">
    <PageHeader title="创建比标任务" description="上传 2~5 份标书，可选上传招标文件用于强制性条款校验">
      <template #extra>
        <a-button size="small" @click="emit('cancel')">
          <ArrowLeftOutlined /> 返回列表
        </a-button>
      </template>
    </PageHeader>

    <SectionCard title="任务信息" class="task-create__card">
      <a-form layout="vertical" class="task-create__form">
        <a-form-item label="任务名称" required>
          <a-input v-model:value="name" placeholder="如：智慧航道疏浚工程比标" :maxlength="50" show-count />
        </a-form-item>

        <a-form-item label="标书文件（2~5 份，PDF / Word）" required>
          <a-upload-dragger
            v-model:file-list="bidFileList"
            multiple
            accept=".pdf,.doc,.docx"
            :before-upload="beforeBidUpload"
          >
            <p class="ant-upload-drag-icon"><InboxOutlined /></p>
            <p class="ant-upload-text">点击或拖拽标书文件到此区域</p>
            <p class="ant-upload-hint">支持 .pdf / .doc / .docx，单份通常 100~500 页</p>
          </a-upload-dragger>
        </a-form-item>

        <a-form-item label="招标文件（可选，用于 AI 提取强制性条款）">
          <a-upload-dragger
            v-model:file-list="tenderFileList"
            :max-count="1"
            accept=".pdf,.doc,.docx"
            :before-upload="beforeTenderUpload"
          >
            <p class="ant-upload-drag-icon"><FileAddOutlined /></p>
            <p class="ant-upload-text">点击或拖拽招标文件到此区域</p>
          </a-upload-dragger>
        </a-form-item>

        <div class="task-create__footer">
          <a-button @click="emit('cancel')">取消</a-button>
          <a-button type="primary" :loading="submitting" :disabled="bidFileList.length < 2" @click="handleSubmit">
            创建并分析
          </a-button>
        </div>
      </a-form>
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { message, Upload } from 'ant-design-vue'
import type { UploadFile } from 'ant-design-vue'
import { ArrowLeftOutlined, InboxOutlined, FileAddOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'

export interface TaskCreatePayload {
  name: string
  bidFiles: File[]
  tenderFile: File | null
}

defineProps<{ submitting: boolean }>()

const emit = defineEmits<{
  submit: [payload: TaskCreatePayload]
  cancel: []
}>()

const name = ref('')
const bidFileList = ref<UploadFile[]>([])
const tenderFileList = ref<UploadFile[]>([])

const ACCEPT = ['.pdf', '.doc', '.docx']

function checkExt(fileName: string): boolean {
  const ext = `.${fileName.split('.').pop()?.toLowerCase()}`
  return ACCEPT.includes(ext)
}

function beforeBidUpload(file: UploadFile): string | boolean {
  if (!checkExt(file.name)) {
    message.warning('仅支持 .pdf / .doc / .docx 格式')
    return Upload.LIST_IGNORE
  }
  if (bidFileList.value.length >= 5) {
    message.warning('标书最多上传 5 份')
    return Upload.LIST_IGNORE
  }
  return false // 不自动上传，创建时统一提交
}

function beforeTenderUpload(file: UploadFile): string | boolean {
  if (!checkExt(file.name)) {
    message.warning('仅支持 .pdf / .doc / .docx 格式')
    return Upload.LIST_IGNORE
  }
  return false
}

function toRawFile(f: UploadFile): File | null {
  return (f.originFileObj as File | undefined) ?? null
}

function handleSubmit(): void {
  if (!name.value.trim()) {
    message.warning('请填写任务名称')
    return
  }
  const bidFiles = bidFileList.value.map(toRawFile).filter((f): f is File => f !== null)
  if (bidFiles.length < 2) {
    message.warning('请至少上传 2 份标书')
    return
  }
  const tenderFile = tenderFileList.value[0] ? toRawFile(tenderFileList.value[0]) : null
  emit('submit', { name: name.value.trim(), bidFiles, tenderFile })
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.task-create__card { max-width: 720px; }

.task-create__form {
  :deep(.ant-form-item) { margin-bottom: @spacing-base; }
}

.task-create__footer {
  display: flex;
  justify-content: flex-end;
  gap: @spacing-sm;
  margin-top: @spacing-xl;
}
</style>
```

- [ ] **Step 2: 修改 `user-web/src/views/ai-bid/compare/index.vue`**

template 中 `TaskList` 分支之后追加：

```vue
    <TaskCreate
      v-else-if="view === 'create'"
      :submitting="creating"
      @submit="handleCreate"
      @cancel="view = 'list'"
    />
```

script 中 import 区追加：

```ts
import TaskCreate from './components/TaskCreate.vue'
import type { TaskCreatePayload } from './components/TaskCreate.vue'
import { createCompareTask, uploadCompareDocument } from '@/api/modules/compare'
```

script 中 `handleRemove` 之后追加以下函数（完整代码；Task 8 会替换其中的视图跳转部分）：

```ts
// ─── 创建任务 ───
const creating = ref(false)

async function handleCreate(payload: TaskCreatePayload): Promise<void> {
  creating.value = true
  try {
    const task = await createCompareTask({ name: payload.name })
    for (const file of payload.bidFiles) {
      await uploadCompareDocument(task.id, file, 'bid')
    }
    if (payload.tenderFile) {
      await uploadCompareDocument(task.id, payload.tenderFile, 'tender')
    }
    currentTask.value = await getCompareTask(task.id)
    view.value = 'progress' // Task 8 接入进度页与轮询
  } catch {
    message.error('创建任务失败')
  } finally {
    creating.value = false
  }
}
```

- [ ] **Step 3: 验证** — `pnpm run typecheck` 通过。手动走查：比标页点「创建任务」→ 填名称、拖入 2 个 PDF（本地任意 PDF 即可）→ 「创建并分析」按钮在不足 2 份时禁用、满 2 份后可点 → 点击后跳转（进度视图 Task 8 才渲染，本 Task 跳转后空白属预期，走查时返回列表确认新任务出现在列表第一行、状态「解析中」蓝色 tag）。
- [ ] **Step 4: （可选）** `git add -A && git commit -m "feat(user-web): compare task creation upload"`

---

## Task 8 【P1】分析进度页

**Files:**
- Create: `user-web/src/views/ai-bid/compare/components/AnalysisProgress.vue`
- Modify: `user-web/src/views/ai-bid/compare/index.vue`（轮询 + progress 视图分支）

复用读标交互语言：左侧 `DocViewer`（文档预览 + 分步进度条），右侧实时证据列表（spec §7.1.4）。轮询任务状态（1.5s），查重证据先到先展示；有招标文件时 `parsed` 状态下 P1 自动提取并锁定条款草案（代码注释标明，Task 14 替换为人工确认页）。

- [ ] **Step 1: 创建 `user-web/src/views/ai-bid/compare/components/AnalysisProgress.vue`，内容如下（完整文件）**

```vue
<template>
  <div class="analysis-progress">
    <PageHeader :title="task.name" description="系统正在解析与对比标书，查重证据将实时出现在右侧">
      <template #extra>
        <a-button type="primary" :disabled="evidences.length === 0" @click="emit('enterResult')">
          进入结果工作台
        </a-button>
      </template>
    </PageHeader>

    <a-alert
      v-if="task.status === 'parsed' && task.tenderDocId"
      type="info"
      show-icon
      message="条款确认"
      description="正在从招标文件提取强制性条款并锁定（P1 自动锁定演示，P2 将提供人工确认页）"
      class="analysis-progress__alert"
    />

    <a-alert
      v-for="doc in failedDocs"
      :key="doc.id"
      type="error"
      show-icon
      :message="`${doc.shortName}（${doc.fileName}）解析失败`"
      :description="doc.failReason"
      class="analysis-progress__alert"
    />

    <div class="analysis-progress__body">
      <div class="analysis-progress__main">
        <DocViewer :doc="preview" :steps="steps" card-title="文档预览" />
      </div>

      <div class="analysis-progress__side">
        <SectionCard title="实时证据" flush class="analysis-progress__evidence-card">
          <a-skeleton v-if="task.status === 'parsing'" :paragraph="{ rows: 3 }" />

          <EmptyState
            v-else-if="evidences.length === 0"
            type="no-data"
            title="暂无证据"
            description="两两查重完成后，证据将实时出现在这里"
          />

          <transition-group v-else name="ev-stagger" tag="div" class="ev-list">
            <div
              v-for="(ev, i) in evidences"
              :key="ev.id"
              class="ev-card"
              :style="{ borderLeftColor: SEVERITY_COLORS[ev.severity], transitionDelay: `${i * 0.04}s` }"
            >
              <div class="ev-card__header">
                <a-tag :color="SEVERITY_COLORS[ev.severity]">{{ SEVERITY_LABELS[ev.severity] }}</a-tag>
                <span class="ev-card__type">{{ EVIDENCE_TYPE_LABELS[ev.type] }}</span>
                <a-tag v-if="ev.aiGenerated" color="purple" class="ev-card__ai">AI 分析</a-tag>
              </div>
              <div class="ev-card__title">{{ ev.title }}</div>
              <div class="ev-card__desc">{{ ev.description }}</div>
            </div>
          </transition-group>
        </SectionCard>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import EmptyState from '@shared/web/components/EmptyState.vue'
import DocViewer from '@shared/web/components/DocViewer.vue'
import type { DocProgressStep } from '@shared/web/components/DocViewer.vue'
import type { CompareTaskDetail, Evidence } from '@/types'
import { EVIDENCE_TYPE_LABELS, SEVERITY_COLORS, SEVERITY_LABELS } from '../constants'

const props = defineProps<{
  task: CompareTaskDetail
  evidences: Evidence[]
  preview: { title: string, content: string } | null
}>()

const emit = defineEmits<{ enterResult: [] }>()

const steps = computed<DocProgressStep[]>(() =>
  props.task.progress.map((p) => ({ title: p.label, status: p.status, progress: p.percent })),
)

const failedDocs = computed(() => props.task.documents.filter((d) => d.parseStatus === 'failed'))
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.analysis-progress__alert { margin-bottom: @spacing-md; }

.analysis-progress__body {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 380px;
  gap: @spacing-xl;
  align-items: stretch;
}

.analysis-progress__main { min-width: 0; }

.analysis-progress__evidence-card {
  height: 100%;
  :deep(.ant-skeleton) { padding: @spacing-md @spacing-xl; }
}

.ev-list {
  display: flex;
  flex-direction: column;
  gap: @spacing-md;
  padding-top: @spacing-md;
}

.ev-card {
  background: @card-bg;
  border: 1px solid @border-color;
  border-left: 3px solid;
  border-radius: @radius-base;
  padding: @spacing-md;
  transition: all @transition-base;
  &:hover { box-shadow: @shadow-md; transform: translateX(2px); }
}

.ev-card__header {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  margin-bottom: @spacing-xs;
}
.ev-card__type { font-size: @font-size-xs; color: @text-tertiary; }
.ev-card__ai { margin-inline-start: auto; }
.ev-card__title {
  font-size: @font-size-sm;
  font-weight: @font-weight-medium;
  color: @text-primary;
  margin-bottom: @spacing-xs;
}
.ev-card__desc {
  font-size: @font-size-xs;
  color: @text-secondary;
  line-height: 1.6;
}

.ev-stagger-enter-active { transition: all 0.3s ease; }
.ev-stagger-enter-from { opacity: 0; transform: translateX(-12px); }

@media (prefers-reduced-motion: reduce) {
  .ev-card,
  .ev-card:hover { transition: none; transform: none; }
  .ev-stagger-enter-active { transition: none; }
}
</style>
```

- [ ] **Step 2: 修改 `user-web/src/views/ai-bid/compare/index.vue`**

script import 区追加：

```ts
import AnalysisProgress from './components/AnalysisProgress.vue'
import {
  getCompareEvidences,
  getCompareIr,
  extractCompareClauses,
  lockCompareClauses,
} from '@/api/modules/compare'
import type { Evidence } from '@/types'
```

将 Task 7 的 `handleCreate` 中 `view.value = 'progress' // Task 8 接入进度页与轮询` 一行替换为：

```ts
    evidences.value = []
    void loadPreview(currentTask.value)
    view.value = 'progress'
    startPolling()
```

`handleCreate` 之后追加以下代码（完整代码）：

```ts
// ─── 分析进度（轮询驱动） ───
const evidences = ref<Evidence[]>([])
const previewDoc = ref<{ title: string, content: string } | null>(null)
let clauseAutoLocking = false

async function refreshEvidences(): Promise<void> {
  if (!currentTask.value) return
  try {
    evidences.value = await getCompareEvidences(currentTask.value.id)
  } catch {
    // 证据尚未产出时静默，下一轮轮询继续
  }
}

async function loadPreview(task: CompareTaskDetail): Promise<void> {
  const firstBid = task.documents.find((d) => d.role === 'bid')
  if (!firstBid) return
  try {
    const ir = await getCompareIr(task.id, firstBid.id)
    previewDoc.value = { title: firstBid.fileName, content: ir.blocks.map((b) => b.text).join('\n\n') }
  } catch {
    previewDoc.value = null
  }
}

function startPolling(): void {
  stopPolling()
  pollTimer = setInterval(async () => {
    if (!currentTask.value) return
    try {
      const task = await getCompareTask(currentTask.value.id)
      currentTask.value = task
      // P1 占位流程：有招标文件时自动提取并锁定条款草案（spec §3.2 要求用户确认后锁定，
      // Task 14（P2）将替换为条款确认页人工锁定）
      if (task.status === 'parsed' && task.tenderDocId && !clauseAutoLocking) {
        clauseAutoLocking = true
        const draft = await extractCompareClauses(task.id)
        await lockCompareClauses(task.id, draft)
      }
      if (task.status === 'comparing' || task.status === 'analyzing' || task.status === 'done') {
        await refreshEvidences()
      }
      if (task.status === 'done' || task.status === 'failed' || task.status === 'partial') {
        stopPolling()
      }
    } catch {
      // 单次轮询失败不打断，下一轮继续
    }
  }, 1500)
}

/** Task 9 补齐结果数据加载后替换为 async 版本 */
function enterResult(): void {
  view.value = 'result'
}
```

将 `openTask` 整体替换为（完整代码）：

```ts
async function openTask(task: CompareTask): Promise<void> {
  try {
    currentTask.value = await getCompareTask(task.id)
  } catch {
    message.error('加载任务详情失败')
    return
  }
  const status = currentTask.value.status
  if (status === 'done' || status === 'partial' || status === 'failed') {
    // Task 9 接入结果工作台数据加载
    view.value = 'result'
    return
  }
  void loadPreview(currentTask.value)
  await refreshEvidences()
  view.value = 'progress'
  startPolling()
}
```

template 中 `TaskCreate` 分支之后追加：

```vue
    <AnalysisProgress
      v-else-if="view === 'progress' && currentTask"
      :task="currentTask"
      :evidences="evidences"
      :preview="previewDoc"
      @enter-result="enterResult"
    />
```

- [ ] **Step 3: KaTeX 公式渲染（DocViewer md 管线扩展）**

公式块 LaTeX（v2 §2 `math_content`/`formula_body`，按裸公式处理）经 KaTeX 渲染；OCR 坏串降级为红字原文。改动点为既有共享组件 `DocViewer.vue` 的 md 渲染管线——只新增 `$` 定界符处理，向后兼容（dubbing 等既有使用方内容无 `$` 不受影响）。

**Files:**
- Create: `packages/shared/src/web/components/katex.ts`
- Modify: `packages/shared/src/web/components/DocViewer.vue`（`renderedMd` 追加公式处理）

3a. 创建 `packages/shared/src/web/components/katex.ts`，内容如下（完整文件）：

```ts
/**
 * KaTeX 渲染薄封装（公式块 LaTeX，v2 §2 math_content/formula_body，按裸公式处理）。
 * throwOnError:false + strict:'ignore'：OCR 坏串渲染为红字原文，不炸组件。
 * 颜色继承主题文字色（dark/light 无碍）；后续在意首屏体积可改动态 import + 占位重渲。
 */
import katex from 'katex'
import 'katex/dist/katex.min.css'

export function renderLatex(tex: string, displayMode = true): string {
  return katex.renderToString(tex, {
    displayMode,
    throwOnError: false,
    strict: 'ignore',
    output: 'html',
  })
}
```

3b. 修改 `DocViewer.vue`：`renderedMd` computed 整体替换为（仅追加两行 `$` 处理，其余与现状一致；注意 `$$` 必须先于 `$` 匹配）：

```ts
const renderedMd = computed(() => {
  const d = props.doc
  if (!d) return ''
  return d.content
    .replace(/^### (.+)$/gm, '<h3>$1</h3>')
    .replace(/^## (.+)$/gm, '<h2>$1</h2>')
    .replace(/^# (.+)$/gm, '<h1>$1</h1>')
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\$\$([^$]+)\$\$/g, (_, tex: string) => renderLatex(tex, true))
    .replace(/\$([^$\n]+)\$/g, (_, tex: string) => renderLatex(tex, false))
    .replace(/\n/g, '<br>')
})
```

script import 区追加：

```ts
import { renderLatex } from './katex'
```

3c. 走查补充（并入 Step 4 验证清单）：mock IR 中本无公式块——临时把 `compare.ts` 某 para 块 text 改为 `'$$E=mc^2$$'`，进度页预览应渲染为排版公式；再改为 `'$$\frac{'`（坏串）应显示红字原文而非白屏；验证后改回。

- [ ] **Step 4: 验证** — `pnpm run typecheck` 通过。手动走查：
  1. 列表点「港区护岸修复工程比标」（analyzing）→ 进入进度页：左侧 DocViewer 顶部进度条「文档解析/两两查重/条款校验」绿色完成、「AI 分析」蓝色 45%；右侧实时证据出现卡片（高/中风险色左边条 + 严重度 tag）；「进入结果工作台」可点（结果页 Task 9 才渲染，点击空白属预期，返回列表继续）。
  2. 列表点「锚地疏浚维护比标」（partial）→ 跳结果视图（空白，Task 9 生效）。
  3. 创建任务（含 1 份招标文件 + 2 份标书）→ 进度页顶部先出现「条款确认」info alert，约 2~3s 后消失并进入查重，右侧证据陆续出现；状态最终停在「AI 分析中/已完成」。
- [ ] **Step 5: （可选）** `git add -A && git commit -m "feat(user-web): compare analysis progress view"`

---

## Task 9 【P1】结果工作台（概览 + 相似度热力图 + 证据清单）

**Files:**
- Modify: `packages/shared/src/web/components/ChartContainer.vue`（注册 heatmap/visualMap，透传图表点击事件）
- Create: `user-web/src/views/ai-bid/compare/components/EvidenceTable.vue`
- Create: `user-web/src/views/ai-bid/compare/components/ResultWorkbench.vue`
- Modify: `user-web/src/views/ai-bid/compare/index.vue`（result 视图分支 + 结果数据加载）

热力图惯例（`docs/chart-conventions.md` 缺失，按现有代码提炼）：`ChartContainer` 容器 + `useChartTheme()` 取 axis/tooltip/legend 色 + `useCssVar()` 取品牌/语义色 + `animationDuration: 600 / easeOutQuad`。点单元格跳到对应文档对的证据（spec §7.1.5）。

- [ ] **Step 1: 整体替换 `packages/shared/src/web/components/ChartContainer.vue` 为以下内容（完整文件；仅新增 HeatmapChart/VisualMapComponent 注册与 `chartClick` 事件透传，其余与现状一致）**

```vue
<template>
  <div class="chart-container" :style="{ height }">
    <div v-if="loading" class="chart-skeleton" aria-hidden="true">
      <div
        v-for="(h, i) in barHeights"
        :key="i"
        class="chart-skeleton-bar"
        :style="{ height: h, animationDelay: `${i * 0.12}s` }"
      />
    </div>
    <VChart v-else :option="option" autoresize class="chart" @click="onChartClick" />
  </div>
</template>

<script setup lang="ts">
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart, BarChart, PieChart, HeatmapChart } from 'echarts/charts'
import {
  TitleComponent,
  TooltipComponent,
  LegendComponent,
  GridComponent,
  DataZoomComponent,
  VisualMapComponent,
} from 'echarts/components'
import VChart from 'vue-echarts'

defineProps<{
  option: Record<string, unknown>
  height?: string
  loading?: boolean
}>()

const emit = defineEmits<{
  /** ECharts 点击事件透传（热力图单元格跳转等交互用） */
  chartClick: [params: Record<string, unknown>]
}>()

function onChartClick(params: Record<string, unknown>): void {
  emit('chartClick', params)
}

use([
  CanvasRenderer,
  LineChart,
  BarChart,
  PieChart,
  HeatmapChart,
  TitleComponent,
  TooltipComponent,
  LegendComponent,
  GridComponent,
  DataZoomComponent,
  VisualMapComponent,
])

const barHeights = ['42%', '68%', '55%', '82%', '60%', '74%', '48%', '64%']
</script>

<style scoped lang="less">
@import '../styles/variables.less';

.chart-container {
  width: 100%;
  position: relative;
}

// 加载态：shimmer 柱状占位条，替代居中 spinner
.chart-skeleton {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: flex-end;
  gap: @spacing-sm;
  padding: @spacing-base @spacing-xl @spacing-xl;
  box-sizing: border-box;
}
.chart-skeleton-bar {
  flex: 1;
  border-radius: @radius-sm @radius-sm 0 0;
  background: @surface-hover;
  animation: chart-shimmer 1.8s ease-in-out infinite;
}
@keyframes chart-shimmer {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.35; }
}

.chart {
  width: 100%;
  height: 100%;
}
</style>
```

- [ ] **Step 2: 创建 `user-web/src/views/ai-bid/compare/components/EvidenceTable.vue`，内容如下（完整文件）**

```vue
<template>
  <SectionCard title="证据清单" flush class="evidence-table">
    <template #extra>
      <div class="evidence-table__filters">
        <a-select
          v-model:value="typeFilter"
          allow-clear
          placeholder="类型"
          style="width: 140px"
          :options="typeOptions"
        />
        <a-radio-group v-model:value="severityFilter" size="small" button-style="solid">
          <a-radio-button value="all">全部</a-radio-button>
          <a-radio-button value="high">高</a-radio-button>
          <a-radio-button value="mid">中</a-radio-button>
          <a-radio-button value="low">低</a-radio-button>
        </a-radio-group>
      </div>
    </template>

    <a-skeleton v-if="loading" :paragraph="{ rows: 4 }" class="evidence-table__skeleton" />

    <EmptyState v-else-if="filtered.length === 0" type="no-data" title="暂无证据" description="调整筛选条件或等待分析完成" />

    <a-table
      v-else
      size="small"
      :data-source="filtered"
      :columns="columns"
      :pagination="{ pageSize: 10, showTotal: (t: number) => `共 ${t} 条` }"
      row-key="id"
      :scroll="{ x: 900 }"
      :custom-row="customRow"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'type'">
          <a-tag>{{ EVIDENCE_TYPE_LABELS[record.type as Evidence['type']] }}</a-tag>
        </template>
        <template v-else-if="column.key === 'severity'">
          <a-tag :color="SEVERITY_COLORS[record.severity as Evidence['severity']]">
            {{ SEVERITY_LABELS[record.severity as Evidence['severity']] }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'title'">
          <div class="evidence-table__summary">
            <div class="evidence-table__title">{{ record.title }}</div>
            <div class="evidence-table__desc">{{ record.description }}</div>
          </div>
        </template>
        <template v-else-if="column.key === 'docs'">{{ docNames(record as Evidence) }}</template>
        <template v-else-if="column.key === 'aiGenerated'">
          <a-tag v-if="record.aiGenerated" color="purple">AI 分析</a-tag>
          <a-tag v-else>算法</a-tag>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-button type="link" size="small" @click="emit('select', record as Evidence)">
            查看对比
          </a-button>
        </template>
      </template>
    </a-table>
  </SectionCard>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import EmptyState from '@shared/web/components/EmptyState.vue'
import type { CompareDocument, Evidence } from '@/types'
import { EVIDENCE_TYPE_LABELS, SEVERITY_COLORS, SEVERITY_LABELS } from '../constants'

const props = defineProps<{
  evidences: Evidence[]
  documents: CompareDocument[]
  loading: boolean
}>()

const emit = defineEmits<{ select: [evidence: Evidence] }>()

const typeFilter = ref<Evidence['type'] | undefined>(undefined)
const severityFilter = ref<'all' | Evidence['severity']>('all')

const typeOptions = (Object.keys(EVIDENCE_TYPE_LABELS) as Evidence['type'][])
  .map((t) => ({ label: EVIDENCE_TYPE_LABELS[t], value: t }))

const filtered = computed(() =>
  props.evidences.filter((e) =>
    (!typeFilter.value || e.type === typeFilter.value)
    && (severityFilter.value === 'all' || e.severity === severityFilter.value),
  ),
)

function docNames(ev: Evidence): string {
  return ev.docIds
    .map((id) => props.documents.find((d) => d.id === id)?.shortName ?? id)
    .join(' × ')
}

function customRow(record: Evidence): Record<string, unknown> {
  return {
    style: { cursor: 'pointer' },
    onClick: () => emit('select', record),
  }
}

const columns = [
  { title: '类型', dataIndex: 'type', key: 'type', width: 100 },
  { title: '严重度', dataIndex: 'severity', key: 'severity', width: 90 },
  { title: '摘要', dataIndex: 'title', key: 'title' },
  { title: '涉及文档', key: 'docs', width: 160 },
  { title: '来源', dataIndex: 'aiGenerated', key: 'aiGenerated', width: 100 },
  { title: '操作', key: 'action', width: 110 },
]
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.evidence-table__filters {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
}

.evidence-table__skeleton { padding: @spacing-md @spacing-xl; }

.evidence-table__summary { text-align: left; }
.evidence-table__title {
  font-size: @font-size-sm;
  font-weight: @font-weight-medium;
  color: @text-primary;
}
.evidence-table__desc {
  font-size: @font-size-xs;
  color: @text-tertiary;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 460px;
}
</style>
```

- [ ] **Step 3: 创建 `user-web/src/views/ai-bid/compare/components/ResultWorkbench.vue`，内容如下（完整文件，P1 版：概览 + 热力图 + 证据清单；Task 15 将增加条款响应矩阵/指标比选 tabs，Task 16 增加导出按钮）**

```vue
<template>
  <div class="result-workbench">
    <PageHeader :title="task.name" :description="report?.summary || '比标结果工作台'">
      <template #extra>
        <a-button size="small" @click="emit('back')">
          <ArrowLeftOutlined /> 返回列表
        </a-button>
      </template>
    </PageHeader>

    <a-alert
      v-for="doc in ocrWarnDocs"
      :key="doc.id"
      type="warning"
      show-icon
      :message="`${doc.shortName}（${doc.fileName}）为扫描件（OCR 低置信页占比 ${Math.round(doc.ocrLowConfidenceRatio * 100)}%），查重结果可能偏差`"
      class="result-workbench__alert"
    />

    <a-row :gutter="16" class="result-workbench__metrics">
      <a-col :span="6">
        <MetricCard title="标书份数" :value="bidCount" suffix="份" icon="FileTextOutlined" :color="brandColor" />
      </a-col>
      <a-col :span="6">
        <MetricCard title="高风险" :value="highCount" suffix="项" icon="WarningOutlined" :color="dangerColor" />
      </a-col>
      <a-col :span="6">
        <MetricCard title="中风险" :value="midCount" suffix="项" icon="AlertOutlined" :color="warningColor" />
      </a-col>
      <a-col :span="6">
        <MetricCard title="条款不响应" :value="noncompliantCount" suffix="项" icon="FileProtectOutlined" :color="accentColor" />
      </a-col>
    </a-row>

    <SectionCard title="相似度矩阵" class="result-workbench__section">
      <template #extra>
        <span class="result-workbench__hint">点击单元格查看该文档对的证据</span>
      </template>
      <ChartContainer
        :option="heatmapOption"
        height="320px"
        :loading="loading"
        @chart-click="onCellClick"
      />
    </SectionCard>

    <EvidenceTable
      :evidences="evidences"
      :documents="task.documents"
      :loading="loading"
      @select="emit('selectEvidence', $event)"
    />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { ArrowLeftOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import MetricCard from '@shared/web/components/MetricCard.vue'
import ChartContainer from '@shared/web/components/ChartContainer.vue'
import { useCssVar } from '@shared/web/composables/useCssVar'
import { useChartTheme } from '@shared/web/composables/useChartTheme'
import EvidenceTable from './EvidenceTable.vue'
import type { CompareReport, CompareTaskDetail, Evidence, SimilarityMatrix } from '@/types'

const props = defineProps<{
  task: CompareTaskDetail
  evidences: Evidence[]
  matrix: SimilarityMatrix | null
  report: CompareReport | null
  loading: boolean
}>()

const emit = defineEmits<{
  selectEvidence: [evidence: Evidence]
  selectPair: [docA: string, docB: string]
  back: []
}>()

const { chartTheme } = useChartTheme()
const brandColor = useCssVar('--color-brand')
const dangerColor = useCssVar('--color-danger')
const warningColor = useCssVar('--color-warning')
const accentColor = useCssVar('--color-accent')

const bidCount = computed(() => props.task.documents.filter((d) => d.role === 'bid').length)
const highCount = computed(() => props.evidences.filter((e) => e.severity === 'high').length)
const midCount = computed(() => props.evidences.filter((e) => e.severity === 'mid').length)
const noncompliantCount = computed(() =>
  (props.report?.clauseResponses ?? []).filter((r) => r.status === 'noncompliant').length,
)
const ocrWarnDocs = computed(() => props.task.documents.filter((d) => d.ocrLowConfidenceRatio > 0.3))

const heatmapOption = computed<Record<string, unknown>>(() => {
  const m = props.matrix
  if (!m) return {}
  const t = chartTheme()
  const labels = m.docIds.map((id) => props.task.documents.find((d) => d.id === id)?.shortName ?? id)
  // 对角线（自相似 1.0）不参与展示，避免误导
  const data: [number, number, number][] = []
  m.values.forEach((row, i) => row.forEach((v, j) => {
    if (i !== j) data.push([j, i, v])
  }))
  return {
    tooltip: {
      backgroundColor: t.tooltipBg,
      borderColor: t.tooltipBorder,
      borderWidth: 1,
      textStyle: { color: t.tooltipColor, fontSize: 13 },
      formatter: (p: { value: [number, number, number] }) =>
        `${labels[p.value[1]]} × ${labels[p.value[0]]}<br/>相似度：${p.value[2].toFixed(2)}`,
    },
    grid: { left: 60, right: 40, top: 16, bottom: 64 },
    xAxis: {
      type: 'category', data: labels,
      axisLine: { show: false }, axisTick: { show: false },
      axisLabel: { color: t.axisColor, fontSize: 12 },
      splitArea: { show: true },
    },
    yAxis: {
      type: 'category', data: labels,
      axisLine: { show: false }, axisTick: { show: false },
      axisLabel: { color: t.axisColor, fontSize: 12 },
      splitArea: { show: true },
    },
    visualMap: {
      min: 0, max: 1, calculable: true,
      orient: 'horizontal', left: 'center', bottom: 0,
      textStyle: { color: t.legendColor, fontSize: 11 },
      inRange: { color: [brandColor.value, warningColor.value, dangerColor.value] },
    },
    series: [{
      type: 'heatmap',
      data,
      label: {
        show: true, fontSize: 12, color: t.tooltipColor,
        formatter: (p: { value: [number, number, number] }) => p.value[2].toFixed(2),
      },
      itemStyle: { borderColor: t.tooltipBg, borderWidth: 2, borderRadius: 4 },
      emphasis: { itemStyle: { shadowBlur: 8, shadowColor: 'rgba(0, 0, 0, 0.25)' } },
      animationDuration: 600,
      animationEasing: 'easeOutQuad',
    }],
  }
})

function onCellClick(params: Record<string, unknown>): void {
  const m = props.matrix
  if (!m) return
  const value = params.value as [number, number, number] | undefined
  if (!value) return
  emit('selectPair', m.docIds[value[0]], m.docIds[value[1]])
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.result-workbench__alert { margin-bottom: @spacing-md; }
.result-workbench__metrics { margin-bottom: @spacing-xl; }
.result-workbench__section { margin-bottom: @spacing-xl; }
.result-workbench__hint {
  font-size: @font-size-xs;
  color: @text-tertiary;
}
</style>
```

- [ ] **Step 4: 修改 `user-web/src/views/ai-bid/compare/index.vue`**

script import 区追加：

```ts
import ResultWorkbench from './components/ResultWorkbench.vue'
import { getCompareMatrix, getCompareReport } from '@/api/modules/compare'
import type { CompareReport, SimilarityMatrix } from '@/types'
```

将 Task 8 的 `enterResult` 函数整体替换为以下代码（完整代码）：

```ts
// ─── 结果工作台 ───
const matrix = ref<SimilarityMatrix | null>(null)
const report = ref<CompareReport | null>(null)
const resultLoading = ref(false)

async function loadResultData(): Promise<void> {
  if (!currentTask.value) return
  resultLoading.value = true
  try {
    const [ev, mx, rp] = await Promise.all([
      getCompareEvidences(currentTask.value.id),
      getCompareMatrix(currentTask.value.id),
      getCompareReport(currentTask.value.id),
    ])
    evidences.value = ev
    matrix.value = mx
    report.value = rp
  } catch {
    message.error('加载结果数据失败')
  } finally {
    resultLoading.value = false
  }
}

async function enterResult(): Promise<void> {
  await loadResultData()
  view.value = 'result'
}

/** Task 13 实现左右对比视图后替换为跳转逻辑 */
function handleSelectEvidence(_ev: Evidence): void {}
function handleSelectPair(_docA: string, _docB: string): void {}
function backToList(): void {
  stopPolling()
  currentTask.value = null
  view.value = 'list'
  void loadTasks()
}
```

将 `openTask` 中 `if (status === 'done' || status === 'partial' || status === 'failed') { ... }` 分支整体替换为：

```ts
  if (status === 'done' || status === 'partial' || status === 'failed') {
    await loadResultData()
    view.value = 'result'
    return
  }
```

template 中 `AnalysisProgress` 分支之后追加：

```vue
    <ResultWorkbench
      v-else-if="view === 'result' && currentTask"
      :task="currentTask"
      :evidences="evidences"
      :matrix="matrix"
      :report="report"
      :loading="resultLoading"
      @select-evidence="handleSelectEvidence"
      @select-pair="handleSelectPair"
      @back="backToList"
    />
```

- [ ] **Step 5: 验证** — `pnpm run typecheck` 通过。手动走查：
  1. 列表点「智慧航道疏浚工程比标」→ 结果工作台：顶部 4 张 MetricCard（标书份数 3 / 高风险 3 / 中风险 2 / 条款不响应 1）；标书B 扫描件 warning alert 出现；相似度热力图 3×3、对角线留空、A×B 单元格 0.87 偏红、C 相关偏蓝，底部 visualMap 可拖选。
  2. 证据清单：6 条；类型筛选「雷同」剩 2 条；严重度点「高」剩 3 条；行 hover 有指针。
  3. 「返回列表」回到列表。
  4. 点热力图 A×B 单元格 / 证据行（跳转 Task 13 生效，本 Task 无反应属预期）。
- [ ] **Step 6: （可选）** `git add -A && git commit -m "feat(user-web): compare result workbench with heatmap"`

---

## Task 10 【P1】pdfjs-dist 依赖与演示 PDF 生成

**Files:**
- Modify: `user-web/package.json`（通过 pnpm 命令新增 `pdfjs-dist` 依赖）
- Create: `user-web/scripts/gen-compare-sample-pdf.mjs`
- Create（脚本产物）: `user-web/public/mock/compare/bid-a.pdf`、`bid-b.pdf`、`bid-c.pdf`、`tender.pdf`、`report-demo.pdf`

pdf.js worker 采用 `import workerSrc from 'pdfjs-dist/build/pdf.worker.min.mjs?url'`（Task 11 移植的 `PdfViewer.vue` 内接入，与 AnGIneer 源写法一致），vite 原生处理，**无需改 `vite.config.ts`**。演示 PDF 由脚本手写最小 PDF 结构生成（无第三方依赖），中文用 Type0 字体 `STSong-Light + UniGB-UCS2-H` 预定义 CMap（pdf.js 回退系统中文字体渲染）。页面 595×842 pt；Task 3 mock IR 的 bbox 为 0~1 归一化值 = 文本行坐标 ÷ 页面尺寸（v2 契约，PDF_Viewer 直接还原；y 为行顶坐标，PDF 绘制时换算 `Tm y = 842 - y - size`）。**移植 PDF_Viewer 后本脚本仍保留**：Task 13 的 bbox 对齐精度走查依赖这组「坐标已知的 PDF + mock IR」对照数据，且 PdfViewer 的虚拟滚动/缩放功能验证也需要多页 PDF。

- [ ] **Step 1: 仓库根执行 `pnpm --filter user-web add pdfjs-dist katex`**

- [ ] **Step 2: 创建 `user-web/scripts/gen-compare-sample-pdf.mjs`，内容如下（完整文件）**

```js
/**
 * 生成比标模块演示 PDF（无第三方依赖，手写最小 PDF 结构）。
 * 中文使用预定义 CMap 的 Type0 字体（STSong-Light + UniGB-UCS2-H），
 * pdf.js 会用系统中文字体回退渲染。
 * 页面尺寸 595×842 pt（A4 @72dpi），与 packages/shared/src/mock/data/compare.ts
 * 中 IR 的 pages[].width/height 一致；每行文本 (x, y, size) 与对应 block 的
 * bbox 对齐：mock IR 存储 0~1 归一化值（行坐标 ÷ 页面尺寸，v2 契约），
 * y 为行顶坐标，PDF 绘制时 Tm y = 842 - y - size。
 * 用法：node user-web/scripts/gen-compare-sample-pdf.mjs
 */
import { writeFileSync, mkdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'

const OUT_DIR = join(dirname(fileURLToPath(import.meta.url)), '../public/mock/compare')
mkdirSync(OUT_DIR, { recursive: true })

const PAGE_W = 595
const PAGE_H = 842

/** 文本 → UTF-16BE（带 BOM）hex 字符串（UniGB-UCS2-H 编码要求） */
function hexText(str) {
  const le = Buffer.from('﻿' + str, 'utf16le') // ﻿ 为 BOM 字符；若编辑器将其吞掉，改写为 Buffer.from('\uFEFF' + str, 'utf16le')
  const be = Buffer.alloc(le.length)
  for (let i = 0; i < le.length; i += 2) {
    be[i] = le[i + 1]
    be[i + 1] = le[i]
  }
  return `<${be.toString('hex').toUpperCase()}>`
}

/** 组装最小 PDF。pages: { x, y, size, text }[][]（y 为行顶坐标） */
function buildPdf(pages) {
  const objects = new Map()
  objects.set(3, '<< /Type /Font /Subtype /Type0 /BaseFont /STSong-Light /Encoding /UniGB-UCS2-H /DescendantFonts [ << /Type /Font /Subtype /CIDFontType0 /BaseFont /STSong-Light /CIDSystemInfo << /Registry (Adobe) /Ordering (GB1) /Supplement 5 >> /DW 1000 >> ] >>')
  const kids = []
  pages.forEach((lines, i) => {
    const pageNum = 4 + i * 2
    const contentNum = pageNum + 1
    kids.push(`${pageNum} 0 R`)
    const stream = lines
      .map((l) => `BT /F1 ${l.size} Tf 1 0 0 1 ${l.x} ${PAGE_H - l.y - l.size} Tm ${hexText(l.text)} Tj ET`)
      .join('\n')
    objects.set(pageNum, `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${PAGE_W} ${PAGE_H}] /Resources << /Font << /F1 3 0 R >> >> /Contents ${contentNum} 0 R >>`)
    objects.set(contentNum, `<< /Length ${stream.length} >>\nstream\n${stream}\nendstream`)
  })
  objects.set(1, '<< /Type /Catalog /Pages 2 0 R >>')
  objects.set(2, `<< /Type /Pages /Kids [ ${kids.join(' ')} ] /Count ${pages.length} >>`)

  let out = '%PDF-1.4\n'
  const offsets = new Map()
  const max = Math.max(...objects.keys())
  for (let n = 1; n <= max; n++) {
    offsets.set(n, out.length)
    out += `${n} 0 obj\n${objects.get(n)}\nendobj\n`
  }
  const xrefPos = out.length
  out += `xref\n0 ${max + 1}\n0000000000 65535 f \n`
  for (let n = 1; n <= max; n++) {
    out += `${String(offsets.get(n)).padStart(10, '0')} 00000 n \n`
  }
  out += `trailer\n<< /Size ${max + 1} /Root 1 0 R >>\nstartxref\n${xrefPos}\n%%EOF\n`
  return out
}

const SHARED_TECH_1 = '3.1 施工总体部署：本工程投入耙吸式挖泥船两艘，采用分段分条施工法'
const SHARED_TECH_2 = '3.2 疏浚工艺流程：定位→下耙→拖航→溢流→起耙→吹填上岸'
const SHARED_TECH_3 = '3.3 质量控制：执行 JTS 257《疏浚工程质量检验标准》全程自检'

const COVER_TITLE = { x: 60, y: 72, size: 22, text: '智慧航道疏浚工程投标文件' }

const DOCS = {
  'bid-a': [
    [COVER_TITLE, { x: 60, y: 140, size: 12, text: '投标人：中港疏浚有限公司' }, { x: 60, y: 172, size: 12, text: '日期：2026 年 7 月 20 日' }],
    [{ x: 60, y: 60, size: 16, text: '第三章 技术方案' }, { x: 60, y: 110, size: 12, text: SHARED_TECH_1 }, { x: 60, y: 146, size: 12, text: SHARED_TECH_2 }, { x: 60, y: 182, size: 12, text: SHARED_TECH_3 }],
    [{ x: 60, y: 60, size: 16, text: '第五章 商务报价' }, { x: 60, y: 110, size: 12, text: '投标总价：人民币 12,688.88 万元' }, { x: 60, y: 146, size: 12, text: '工期：300 日历天，质保期 2 年' }],
  ],
  'bid-b': [
    [COVER_TITLE, { x: 60, y: 140, size: 12, text: '投标人：长江航道工程局' }, { x: 60, y: 172, size: 12, text: '日期：2026 年 7 月 21 日' }],
    [{ x: 60, y: 60, size: 16, text: '第三章 技术方案' }, { x: 60, y: 110, size: 12, text: SHARED_TECH_1 }, { x: 60, y: 146, size: 12, text: SHARED_TECH_2 }, { x: 60, y: 182, size: 12, text: SHARED_TECH_3 }],
    [{ x: 60, y: 60, size: 16, text: '第五章 商务报价' }, { x: 60, y: 110, size: 12, text: '投标总价：人民币 10,288.88 万元' }, { x: 60, y: 146, size: 12, text: '工期：320 日历天，质保期 2 年' }],
  ],
  'bid-c': [
    [{ x: 60, y: 72, size: 22, text: '智慧航道疏浚工程投标文件（技术标）' }, { x: 60, y: 140, size: 12, text: '投标人：海工建设集团' }, { x: 60, y: 172, size: 12, text: '日期：2026 年 7 月 21 日' }],
    [{ x: 60, y: 60, size: 16, text: '第三章 施工组织设计' }, { x: 60, y: 110, size: 12, text: '3.1 工艺方案：采用绞吸式挖泥船加接力泵站，管线吹填上岸' }, { x: 60, y: 146, size: 12, text: '3.2 进度安排：总工期 280 日历天，分三个施工段流水作业' }],
    [{ x: 60, y: 60, size: 16, text: '第五章 商务报价' }, { x: 60, y: 110, size: 12, text: '投标总价：人民币 13,500.00 万元' }, { x: 60, y: 146, size: 12, text: '工期：280 日历天，质保期 1 年' }],
  ],
  'tender': [
    [{ x: 60, y: 72, size: 22, text: '智慧航道疏浚工程招标文件' }, { x: 60, y: 140, size: 12, text: '招标人：市港航管理局' }, { x: 60, y: 172, size: 12, text: '强制性条款：质保期不少于 2 年' }],
  ],
  'report-demo': [
    [{ x: 60, y: 72, size: 22, text: '智慧航道疏浚工程比标报告' }, { x: 60, y: 140, size: 12, text: '总体结论：标书A 与标书B 存在较高围标嫌疑（相似度 0.87）' }, { x: 60, y: 172, size: 12, text: '高风险 3 项 · 中风险 2 项 · 低风险 1 项（演示导出文件）' }],
  ],
}

for (const [name, pages] of Object.entries(DOCS)) {
  writeFileSync(join(OUT_DIR, `${name}.pdf`), buildPdf(pages), 'latin1')
  console.log(`generated public/mock/compare/${name}.pdf (${pages.length} page(s))`)
}
```

- [ ] **Step 3: 仓库根执行 `node user-web/scripts/gen-compare-sample-pdf.mjs`**，应输出 5 行 `generated ...`，且 `user-web/public/mock/compare/` 下生成 5 个 PDF。
- [ ] **Step 4: 验证** — `pnpm run typecheck` 通过（脚本为 .mjs，不参与类型检查）。手动走查：`pnpm dev` 后浏览器直接访问 `http://localhost:5373/mock/compare/bid-a.pdf`，应显示 3 页中文 PDF（封面/技术方案/商务报价）；若中文不显示，检查系统是否装有中文字体（Windows 默认有，pdf.js 回退渲染）。
- [ ] **Step 5: （可选）** `git add -A && git commit -m "chore(user-web): add pdfjs-dist and demo pdf generator"`

---

## Task 11 【P1】移植 PDF_Viewer 到 packages/shared（复制 + 解耦改造）

**Files:**
- Create: `packages/shared/src/web/components/pdf-viewer/PdfViewer.vue`（复制自 AnGIneer 后按本 Task 清单改造）
- Create: `packages/shared/src/web/components/pdf-viewer/highlight.ts`（坐标归一化纯函数 + `LinkedHighlight` 类型）

路线 A（复制改造，不做独立包）：保持 `PdfViewerController`（源 :288-1282）原样不动，便于日后与 AnGIneer 同步 bugfix；只改 imports/props/emits/模板分支/样式变量这些「壳」。源文件：`D:/AI/AnGIneer/packages/docs-ui/src/components/common/viewers/PDF_Viewer.vue`（1824 行，下文「源 :N」均指该文件行号）。

- [ ] **Step 1: 复制源文件**

```bash
mkdir -p packages/shared/src/web/components/pdf-viewer
cp "D:/AI/AnGIneer/packages/docs-ui/src/components/common/viewers/PDF_Viewer.vue" packages/shared/src/web/components/pdf-viewer/PdfViewer.vue
```

在 `PdfViewer.vue` 的 `<template>` 之前插入移植头注释：

```vue
<!--
  移植自 AnGIneer: packages/docs-ui/src/components/common/viewers/PDF_Viewer.vue（1824 行版本）
  路线 A：复制改造。PdfViewerController 保持与源一致以便同步 bugfix；
  解耦点：props 精简为纯 PDF 查看器、KnowledgeTreeNode 移除、--dp-* 主题变量映射到 DredgeAI CSS 变量、
  高亮支持 per-box 颜色注入（--pdf-hl-color）与 flash 闪烁。
-->
```

- [ ] **Step 2: 创建 `packages/shared/src/web/components/pdf-viewer/highlight.ts`，内容如下（完整文件）**

纯函数复制自 `D:/AI/AnGIneer/packages/docs-ui/src/composables/useWorkspaceLinkage.ts`（行号见各函数注释），零依赖；`LinkedHighlight` 在源接口（:6-21）基础上增加可选 `color`（配对/严重度着色用）。`mapIrBlocksToHighlights` 在 Task 12 追加。

```ts
/**
 * 移植自 AnGIneer: packages/docs-ui/src/composables/useWorkspaceLinkage.ts
 * 仅取坐标归一化纯函数段（:57-78 / :101-140 / :244-262）与 LinkedHighlight 接口（:6-21），
 * 题注推断、知识图谱联动等其余逻辑不搬。
 */

/** PDF 高亮框（归一化坐标 0~1，page 为 1-based）。color 为 DredgeAI 扩展：per-box 配对/严重度着色 */
export interface LinkedHighlight {
  id: string
  itemId: string
  structuredItemId?: string
  page: number
  hasRect: boolean
  left: number
  top: number
  width: number
  height: number
  lineStart: number | null
  lineEnd: number | null
  type?: string
  contdTargetId?: string | null
  tableMergeId?: string | null
  /** DredgeAI 扩展：高亮框颜色（CSS 色值，经 inline style 注入 --pdf-hl-color） */
  color?: string
}

export interface RectBounds {
  left: number
  top: number
  width: number
  height: number
}

// 源 :57-61
const readNumeric = (value: unknown): number | null => {
  const numberValue = Number(value)
  if (!Number.isFinite(numberValue)) return null
  return numberValue
}

// 源 :63-78
const readFirstNumeric = (source: Record<string, any>, keys: string[]): number | null => {
  const readByPath = (payload: Record<string, any>, keyPath: string): unknown => {
    if (!keyPath.includes('.')) return payload[keyPath]
    return keyPath.split('.').reduce<unknown>((value, segment) => {
      if (!value || typeof value !== 'object') return undefined
      return (value as Record<string, any>)[segment]
    }, payload)
  }
  for (const key of keys) {
    const value = readNumeric(readByPath(source, key))
    if (value !== null) {
      return value
    }
  }
  return null
}

// 源 :101-110
export const normalizeRect = (bbox: unknown): RectBounds | null => {
  if (!Array.isArray(bbox) || bbox.length < 4) return null
  const [x0, y0, x1, y1] = bbox
  return {
    left: Math.max(0, Math.min(Number(x0) || 0, Number(x1) || 0)),
    top: Math.max(0, Math.min(Number(y0) || 0, Number(y1) || 0)),
    width: Math.abs((Number(x1) || 0) - (Number(x0) || 0)),
    height: Math.abs((Number(y1) || 0) - (Number(y0) || 0)),
  }
}

// 源 :112-140
export const normalizeRectFromBaseRow = (row: Record<string, any>): RectBounds | null => {
  const directBox = normalizeRect(row.bbox || row.bbox_norm || row.normalized_bbox)
  if (directBox) return directBox
  const x1 = readFirstNumeric(row, ['bbox_norm_x1', 'bbox_norm.left', 'bbox.left'])
  const y1 = readFirstNumeric(row, ['bbox_norm_y1', 'bbox_norm.top', 'bbox.top'])
  const x2 = readFirstNumeric(row, ['bbox_norm_x2', 'bbox_norm.right', 'bbox.right'])
  const y2 = readFirstNumeric(row, ['bbox_norm_y2', 'bbox_norm.bottom', 'bbox.bottom'])
  if (x1 !== null && y1 !== null && x2 !== null && y2 !== null) {
    return normalizeRect([x1, y1, x2, y2])
  }
  const absX1 = readFirstNumeric(row, ['bbox_abs_x1'])
  const absY1 = readFirstNumeric(row, ['bbox_abs_y1'])
  const absX2 = readFirstNumeric(row, ['bbox_abs_x2'])
  const absY2 = readFirstNumeric(row, ['bbox_abs_y2'])
  const pageWidth = readFirstNumeric(row, ['page_width'])
  const pageHeight = readFirstNumeric(row, ['page_height'])
  if (
    absX1 !== null && absY1 !== null && absX2 !== null && absY2 !== null
    && pageWidth !== null && pageHeight !== null && pageWidth > 0 && pageHeight > 0
  ) {
    return normalizeRect([
      absX1 / pageWidth,
      absY1 / pageHeight,
      absX2 / pageWidth,
      absY2 / pageHeight,
    ])
  }
  return null
}

/**
 * 源 :244-262。三种输入自适应：已归一化（max≤1.2）/ 绝对像素+页面尺寸 / 散字段。
 * 比标 IR bbox 已是 0~1 归一化（v2 契约），走「max≤1.2 直收」分支；像素分支仅为兼容保留。
 */
export const normalizeRectFromPayload = (
  payload: Record<string, any>,
  pageWidth?: number | null,
  pageHeight?: number | null,
): RectBounds | null => {
  const rawBox = payload.bbox || payload.box || payload.rect || payload.boundary
  if (Array.isArray(rawBox) && rawBox.length >= 4) {
    const values = rawBox.slice(0, 4).map((value) => Number(value) || 0)
    const maxValue = Math.max(...values.map((value) => Math.abs(value)))
    if (maxValue <= 1.2) {
      return normalizeRect(values)
    }
    if (pageWidth && pageHeight && pageWidth > 0 && pageHeight > 0) {
      return normalizeRect([
        values[0] / pageWidth,
        values[1] / pageHeight,
        values[2] / pageWidth,
        values[3] / pageHeight,
      ])
    }
  }
  return normalizeRectFromBaseRow(payload)
}
```

- [ ] **Step 3: 改造 `PdfViewer.vue` 的 imports / props / emits（解耦核心）**

3a. 删除源 :226 `import type { KnowledgeTreeNode } from '../../../types/tree'`，在同一位置替换为：

```ts
import type { LinkedHighlight } from './highlight'
```

3b. 删除源 :228-241 的组件内 `interface LinkedHighlight { ... }`（已由 highlight.ts 提供），保留 `VirtualPageMeta` / `RenderedPageMetrics` 两个接口。

3c. 将源 :257-282 的 `props` / `emit` 定义整体替换为（纯 PDF 精简形态；`fileUrl` 由调用方传完整 URL，替代 `useWorkspacePreview.ts:59` 的 `/api/files?path=` 拼接）：

```ts
const props = withDefaults(defineProps<{
  /** 完整文件 URL（调用方准备好，组件内不做路径拼接） */
  fileUrl: string
  /** 页首标题 */
  title?: string
  /** 高亮框（归一化坐标，见 ./highlight.ts） */
  highlights?: LinkedHighlight[]
  /** 常亮高亮 id（配对定位） */
  activeHighlightId?: string | null
  /** 闪烁高亮 id（点击证据跳页时由调用方设置，~1.6s 后清除） */
  flashHighlightId?: string | null
  /** 外部跳页（watch → controller.scrollToPdfPage） */
  currentPdfPage?: number
  /** 外部滚动同步 0~1，<0 表示不同步（逐块对齐模式用） */
  scrollPercent?: number
}>(), {
  title: '原文',
  highlights: () => [],
  activeHighlightId: null,
  flashHighlightId: null,
  currentPdfPage: 1,
  scrollPercent: -1,
})

const emit = defineEmits<{
  'select-highlight': [highlight: LinkedHighlight]
  'hover-highlight': [id: string | null]
  /** PDF 滚动比例 0~1（逐块对齐模式同步用，DredgeAI 新增） */
  scroll: [percent: number]
  /** 当前页变化（工具栏翻页/滚动时，DredgeAI 新增） */
  'page-change': [page: number]
}>()

// 移植说明：本组件仅保留 PDF 预览，原 workspace 形态的 isPdf 分支恒真（保持原结构以减小 diff）
const isPdf = true
```

3d. 全局替换：`props.isPdf` → `isPdf`（源 :1311、:1372、:1394、:1401、:1421、:1428 等处）；`props.highlightLinkEnabled` → `true`（源 :1372、:1394，高亮联动在本组件恒开）。controller 内部不引用这两个 prop，无需改动。

- [ ] **Step 4: 模板精简（删除非 PDF 分支，保留原结构）**

4a. 源 :6 `<span class="pane-title-prefix">原文</span>` 改为 `<span class="pane-title-prefix">{{ title }}</span>`；删除源 :8-13 的两个解析状态 `a-tag`。

4b. 删除源 :101-112 整个 `parse-progress-row` 区块（含 `a-steps`）。

4c. 删除源 :185-214（`isOffice` / `isImage` / `isText` / `a-empty` 四个非 PDF 分支），保留 :113-184 的 PDF 分支与 `.file-preview` 容器。

4d. 高亮层（源 :164-179）的 `pdf-highlight-box` div 替换为以下（新增 `flash` class 与 per-box 颜色注入）：

```vue
              <div
                v-for="item in getPageHighlights(pageMeta.page)"
                :key="item.id"
                :class="['pdf-highlight-box', {
                  active: item.id === activeHighlightId || item.itemId === activeHighlightId,
                  flash: item.id === flashHighlightId || item.itemId === flashHighlightId,
                }]"
                :style="{
                  left: `${item.left * 100}%`,
                  top: `${item.top * 100}%`,
                  width: `${item.width * 100}%`,
                  height: `${item.height * 100}%`,
                  ...(item.color ? { '--pdf-hl-color': item.color } : {}),
                }"
                @mouseenter="emit('hover-highlight', item.itemId)"
                @mouseleave="emit('hover-highlight', null)"
                @click="emit('select-highlight', item)"
              >
                <span v-if="getHighlightTypeLabel(item.type)" class="highlight-type-tag">{{ getHighlightTypeLabel(item.type) }}</span>
              </div>
```

- [ ] **Step 5: script setup glue 精简**

5a. 删除 `showNonPdfLoading`（源 :1316-1320）与 `parseStepIndex`（源 :1322-1333）两个 computed；同步删除源 :1336 `void [...]` 占位行中的 `showNonPdfLoading, parseStepIndex` 两个名字。

5b. 删除 `textScrollPercent` watch（源 :1436-1447），替换为（仅保留 PDF 滚动同步，数据源改为 `scrollPercent` prop）：

```ts
watch(() => props.scrollPercent, (percent) => {
  if (percent == null || percent < 0) return
  if (pdfScrollRef.value && !state.isPdfUserScrolling && !useNativePdfPreview.value) {
    state.applyingExternalPdfScroll = true
    const max = pdfScrollRef.value.scrollHeight - pdfScrollRef.value.clientHeight
    pdfScrollRef.value.scrollTop = percent * max
    requestAnimationFrame(() => { state.applyingExternalPdfScroll = false })
  }
})
```

5c. 将 `onPdfScroll`（源 :1456）替换为以下（包装 emit 滚动比例；并在其后追加 `page-change` watch）：

```ts
const onPdfScroll = (e: Event) => {
  controller.onPdfScroll(e)
  const el = pdfScrollRef.value
  if (!el) return
  const max = el.scrollHeight - el.clientHeight
  emit('scroll', max > 0 ? el.scrollTop / max : 0)
}

watch(() => state.activePdfPage, (page) => emit('page-change', page))
```

5d. 删除 `onLeftTextScroll`（源 :1459-1461）；同步从 refs 解构（源 :1292-1299）中删除 `leftText: leftTextRef,` 一行（删除后该变量无人使用，`noUnusedLocals` 会报错）。`currentPdfPage` watch（源 :1427-1434）与 source watch（源 :1400-1418）保留不动。

5e. controller 内 `normalizedPdfSource`（源 :380-382）替换为（`pdfViewerUrl` prop 已删除）：

```ts
  public get normalizedPdfSource() {
    return props.fileUrl
  }
```

5f. controller 内 `displayPdfPageCount` getter 中 `if (props.pdfPageCount && props.pdfPageCount > 1) return props.pdfPageCount` 一行（源 :363，`pdfPageCount` prop 已删除）整行删除，页数完全由 pdf.js 解析结果给出。

controller 其余部分（含 `loadPdfDocument` 三级降级、虚拟滚动、缩放、`scrollToPdfPage`，源 :288-1282）**一行不改**。

- [ ] **Step 6: 样式主题映射与高亮改造**

6a. 全局替换 `--dp-*` 主题变量（AGENTS.md 禁裸 hex、主题色一律引用变量）：

| 源（AnGIneer） | 替换为（DredgeAI） |
|---|---|
| `var(--dp-pane-border)` | `var(--color-border)` |
| `var(--dp-pane-bg)` | `var(--color-card-bg)` |
| `var(--dp-title-text)` | `var(--color-text-secondary)` |
| `var(--dp-title-border)` | `var(--color-divider)` |
| `var(--dp-title-bg)` | `var(--color-content-bg)` |
| `var(--dp-title-strong)` | `var(--color-text-primary)` |
| `var(--dp-progress-bg)` | `var(--color-content-bg)` |
| `var(--dp-sub-text)` | `var(--color-text-tertiary)` |
| `var(--dp-brand-primary)` | `var(--color-brand)` |
| `var(--dp-content-bg)` | `var(--color-content-bg)` |
| `var(--dp-bg)` | `var(--color-content-bg)` |
| `var(--dp-bg-tertiary)` | `var(--color-content-bg)` |

6b. 高亮样式块（源 :1744-1783，硬编码蓝 `rgba(24,144,255,…)` / `rgba(22,119,255,…)`）整体替换为（默认取品牌色，`--pdf-hl-color` 由 inline style 按配对/严重度注入；新增 flash 动画与降级）：

```less
.pdf-highlight-box {
  position: absolute;
  border: 1px solid var(--pdf-hl-color, var(--color-brand));
  background: color-mix(in srgb, var(--pdf-hl-color, var(--color-brand)) 10%, transparent);
  box-shadow: 0 0 0 1px color-mix(in srgb, var(--pdf-hl-color, var(--color-brand)) 14%, transparent);
  border-radius: 4px;
  pointer-events: auto;
  transition: background 0.18s ease, border-color 0.18s ease;
}

.pdf-highlight-box.active {
  background: color-mix(in srgb, var(--pdf-hl-color, var(--color-brand)) 26%, transparent);
  z-index: 10;
}

.pdf-highlight-box.flash { animation: pdf-hl-flash 0.8s ease 2; }

@keyframes pdf-hl-flash {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.3; }
}

@media (prefers-reduced-motion: reduce) {
  .pdf-highlight-box.flash { animation: none; }
}

.highlight-type-tag {
  position: absolute;
  left: 0;
  top: 0;
  max-width: calc(100% - 4px);
  padding: 2px 6px;
  overflow: hidden;
  color: #fff;
  font-size: 10px;
  line-height: 1.2;
  text-overflow: ellipsis;
  white-space: nowrap;
  background: var(--pdf-hl-color, var(--color-brand));
  border-bottom-right-radius: 4px;
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.18s ease;
  z-index: 11;
}

.pdf-highlight-box:hover .highlight-type-tag,
.pdf-highlight-box.active .highlight-type-tag {
  opacity: 1;
}
```

- [ ] **Step 7: 验证** — `pnpm run typecheck` 通过（重点：`PdfViewerController` 除 5e/5f 两行外未动；无 `KnowledgeTreeNode`/`props.node`/`props.isPdf`/`props.pdfViewerUrl`/`props.highlightLinkEnabled`/`props.pdfPageCount` 残留引用，可用 Grep 复核）。本 Task 组件尚未挂载，界面走查在 Task 13 统一进行。
- [ ] **Step 8: （可选，由执行者决定）** `git add -A && git commit -m "feat(shared): port PDF viewer from AnGIneer docs-ui"`

---

## Task 12 【P1】IR→LinkedHighlight 薄 mapper + DiffViewer 双实例编排

**Files:**
- Modify: `packages/shared/src/web/components/pdf-viewer/highlight.ts`（追加 `mapIrBlocksToHighlights`）
- Create: `user-web/src/views/ai-bid/compare/components/DiffViewer.vue`（双 `PdfViewer` 实例编排）

- [ ] **Step 1: `highlight.ts` 末尾追加以下代码（完整代码）**

输入适配：v2 契约下比标 IR 的 `bbox` 已是 0~1 归一化坐标（左上角原点），直接 `normalizeRect` 采用，无需像素换算；`pageIdx`（0-based）转 `page`（1-based）。

```ts
import type { IrDocument } from '../../../core/types'

/**
 * 比标 IR → LinkedHighlight 映射（薄 mapper）。
 * blockColors 由调用方按配对/严重度着色（blockId → CSS 色值），未着色的块不产出高亮。
 * bbox 为 0~1 归一化（v2 契约），直接归一化校验后即可用。
 */
export function mapIrBlocksToHighlights(
  ir: IrDocument | null,
  blockColors: ReadonlyMap<string, string>,
): LinkedHighlight[] {
  if (!ir) return []
  const highlights: LinkedHighlight[] = []
  for (const block of ir.blocks) {
    const color = blockColors.get(block.blockId)
    if (!color) continue
    const rect = normalizeRect(block.bbox)
    highlights.push({
      id: block.blockId,
      itemId: block.blockId,
      page: block.pageIdx + 1,
      hasRect: true,
      ...rect,
      lineStart: null,
      lineEnd: null,
      type: block.type,
      color,
    })
  }
  return highlights
}
```

- [ ] **Step 2: 创建 `user-web/src/views/ai-bid/compare/components/DiffViewer.vue`，内容如下（完整文件）**

编排要点：雷同块左右按 index 配对同色（五色板取主题 CSS 变量）；`select-highlight` → 对侧配对块设 `activeHighlightId`（常亮）+ `flashHighlightId`（闪烁 1.6s 后清除）+ `currentPdfPage`（跳页）；「逐块对齐」用 `scroll`/`scrollPercent` 按比例互同步（带循环保护）。严重度色只用于 header tag（`SEVERITY_COLORS`，AGENTS 配色速查表），高亮框颜色表达「配对关系」而非严重度。

```vue
<template>
  <div class="diff-viewer">
    <div class="diff-viewer__header">
      <div class="diff-viewer__title-row">
        <a-tag :color="SEVERITY_COLORS[evidence.severity]">{{ SEVERITY_LABELS[evidence.severity] }}</a-tag>
        <span class="diff-viewer__title">{{ evidence.title }}</span>
        <a-tag v-if="evidence.aiGenerated" color="purple">AI 分析</a-tag>
        <span v-if="evidence.metrics.similarity != null" class="diff-viewer__metric">
          相似度 {{ evidence.metrics.similarity.toFixed(2) }}
        </span>
      </div>
      <div class="diff-viewer__ops">
        <span class="diff-viewer__align">
          <a-switch v-model:checked="alignMode" size="small" /> 逐块对齐
        </span>
        <a-button size="small" :disabled="!hasPrev" @click="emit('prev')">
          <LeftOutlined /> 上一条
        </a-button>
        <a-button size="small" :disabled="!hasNext" @click="emit('next')">
          下一条 <RightOutlined />
        </a-button>
        <a-button size="small" @click="emit('close')">
          <CloseOutlined /> 返回
        </a-button>
      </div>
    </div>

    <div class="diff-viewer__desc">{{ evidence.description }}</div>

    <a-alert
      v-if="ocrWarn"
      type="warning"
      show-icon
      message="涉及扫描件文档，以下内容来自 OCR 识别，准确率可能受影响"
      class="diff-viewer__alert"
    />

    <div class="diff-viewer__panes">
      <PdfViewer
        :file-url="leftDoc.fileUrl"
        :title="`${leftDoc.shortName} · ${leftDoc.fileName}`"
        :highlights="leftHighlights"
        :active-highlight-id="leftActive"
        :flash-highlight-id="leftFlash"
        :current-pdf-page="leftPage"
        :scroll-percent="leftPercent"
        @select-highlight="onLeftSelect"
        @scroll="onLeftScroll"
      />
      <PdfViewer
        :file-url="rightDoc.fileUrl"
        :title="`${rightDoc.shortName} · ${rightDoc.fileName}`"
        :highlights="rightHighlights"
        :active-highlight-id="rightActive"
        :flash-highlight-id="rightFlash"
        :current-pdf-page="rightPage"
        :scroll-percent="rightPercent"
        @select-highlight="onRightSelect"
        @scroll="onRightScroll"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { LeftOutlined, RightOutlined, CloseOutlined } from '@ant-design/icons-vue'
import PdfViewer from '@shared/web/components/pdf-viewer/PdfViewer.vue'
import { mapIrBlocksToHighlights } from '@shared/web/components/pdf-viewer/highlight'
import type { LinkedHighlight } from '@shared/web/components/pdf-viewer/highlight'
import { useCssVar } from '@shared/web/composables/useCssVar'
import type { CompareDocument, Evidence, IrDocument } from '@/types'
import { SEVERITY_COLORS, SEVERITY_LABELS } from '../constants'

const props = defineProps<{
  evidence: Evidence
  leftDoc: CompareDocument
  rightDoc: CompareDocument
  leftIr: IrDocument | null
  rightIr: IrDocument | null
  hasPrev: boolean
  hasNext: boolean
}>()

const emit = defineEmits<{
  close: []
  prev: []
  next: []
}>()

// 雷同块配对色（左右同 index 同色），取自主题 CSS 变量
const brandColor = useCssVar('--color-brand')
const dangerColor = useCssVar('--color-danger')
const warningColor = useCssVar('--color-warning')
const successColor = useCssVar('--color-success')
const accentColor = useCssVar('--color-accent')
const pairColors = computed(() => [
  brandColor.value, dangerColor.value, warningColor.value, successColor.value, accentColor.value,
])

const leftBlockIds = computed(() => props.evidence.locations[0]?.blockIds ?? [])
const rightBlockIds = computed(() => props.evidence.locations[1]?.blockIds ?? [])

const leftColorMap = computed(() =>
  new Map(leftBlockIds.value.map((id, i) => [id, pairColors.value[i % pairColors.value.length]])),
)
const rightColorMap = computed(() =>
  new Map(rightBlockIds.value.map((id, i) => [id, pairColors.value[i % pairColors.value.length]])),
)

const leftHighlights = computed(() => mapIrBlocksToHighlights(props.leftIr, leftColorMap.value))
const rightHighlights = computed(() => mapIrBlocksToHighlights(props.rightIr, rightColorMap.value))

// 常亮 / 闪烁 / 跳页目标
const leftActive = ref<string | null>(null)
const rightActive = ref<string | null>(null)
const leftFlash = ref<string | null>(null)
const rightFlash = ref<string | null>(null)
const leftPage = ref(1)
const rightPage = ref(1)
const leftPercent = ref(-1)
const rightPercent = ref(-1)
let flashTimer: ReturnType<typeof setTimeout> | undefined

function pageOf(ir: IrDocument | null, blockId: string): number {
  return (ir?.blocks.find((b) => b.blockId === blockId)?.pageIdx ?? 0) + 1
}

/** 聚焦某侧块：常亮 + 闪烁 + 跳页 */
function focusBlock(side: 'left' | 'right', blockId: string): void {
  const ir = side === 'left' ? props.leftIr : props.rightIr
  if (side === 'left') {
    leftActive.value = blockId
    leftFlash.value = blockId
    leftPage.value = pageOf(ir, blockId)
  } else {
    rightActive.value = blockId
    rightFlash.value = blockId
    rightPage.value = pageOf(ir, blockId)
  }
  if (flashTimer) clearTimeout(flashTimer)
  flashTimer = setTimeout(() => {
    leftFlash.value = null
    rightFlash.value = null
  }, 1600)
}

watch(() => props.evidence.id, () => {
  if (leftBlockIds.value[0]) focusBlock('left', leftBlockIds.value[0])
  if (rightBlockIds.value[0]) focusBlock('right', rightBlockIds.value[0])
}, { immediate: true })

// 点一侧高亮框 → 对侧配对块跳页闪烁
function onLeftSelect(item: LinkedHighlight): void {
  const idx = leftBlockIds.value.indexOf(item.itemId)
  if (idx >= 0 && rightBlockIds.value[idx]) focusBlock('right', rightBlockIds.value[idx])
}

function onRightSelect(item: LinkedHighlight): void {
  const idx = rightBlockIds.value.indexOf(item.itemId)
  if (idx >= 0 && leftBlockIds.value[idx]) focusBlock('left', leftBlockIds.value[idx])
}

// 逐块对齐模式：滚动比例互同步（带循环保护）
const alignMode = ref(true)
let syncing = false

function onLeftScroll(percent: number): void {
  if (!alignMode.value || syncing) return
  syncing = true
  rightPercent.value = percent
  requestAnimationFrame(() => { syncing = false })
}

function onRightScroll(percent: number): void {
  if (!alignMode.value || syncing) return
  syncing = true
  leftPercent.value = percent
  requestAnimationFrame(() => { syncing = false })
}

const ocrWarn = computed(() =>
  props.leftDoc.ocrLowConfidenceRatio > 0.3 || props.rightDoc.ocrLowConfidenceRatio > 0.3,
)
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.diff-viewer {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 220px);
  min-height: 480px;
}

.diff-viewer__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: @spacing-md;
  margin-bottom: @spacing-sm;
  flex-wrap: wrap;
}

.diff-viewer__title-row {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  min-width: 0;
}

.diff-viewer__title {
  font-size: @font-size-lg;
  font-weight: @font-weight-semibold;
  color: @text-primary;
}

.diff-viewer__metric {
  font-size: @font-size-sm;
  color: @text-secondary;
  font-variant-numeric: tabular-nums;
}

.diff-viewer__ops {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
}

.diff-viewer__align {
  display: inline-flex;
  align-items: center;
  gap: @spacing-xs;
  font-size: @font-size-sm;
  color: @text-secondary;
  margin-right: @spacing-sm;
}

.diff-viewer__desc {
  font-size: @font-size-sm;
  color: @text-secondary;
  line-height: 1.6;
  margin-bottom: @spacing-md;
}

.diff-viewer__alert { margin-bottom: @spacing-md; }

.diff-viewer__panes {
  flex: 1;
  min-height: 0;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: @spacing-base;
}
</style>
```

- [ ] **Step 3: 验证** — `pnpm run typecheck` 通过。组件尚未挂载，界面走查在 Task 13 统一进行。
- [ ] **Step 4: （可选）** `git add -A && git commit -m "feat(user-web): compare diff viewer with dual ported PDF viewers"`

---

## Task 13 【P1】index.vue 接线（diff 视图分支）与对比视图走查

**Files:**
- Modify: `user-web/src/views/ai-bid/compare/index.vue`（diff 视图分支 + 证据/文档对跳转）

- [ ] **Step 1: 修改 `user-web/src/views/ai-bid/compare/index.vue`**

script import 区追加：

```ts
import { computed } from 'vue'
import DiffViewer from './components/DiffViewer.vue'
import type { CompareDocument, IrDocument } from '@/types'
```

将 Task 9 的两个占位函数 `handleSelectEvidence` / `handleSelectPair` 整体替换为以下代码（完整代码）：

```ts
// ─── 左右对比视图 ───
const activeEvidenceIndex = ref(0)
const diffDocs = ref<{ left: CompareDocument, right: CompareDocument } | null>(null)
const leftIr = ref<IrDocument | null>(null)
const rightIr = ref<IrDocument | null>(null)

const activeEvidence = computed(() => evidences.value[activeEvidenceIndex.value] ?? null)

async function openDiff(index: number): Promise<void> {
  const task = currentTask.value
  const ev = evidences.value[index]
  if (!task || !ev) return
  activeEvidenceIndex.value = index
  const leftId = ev.locations[0]?.docId ?? ev.docIds[0]
  const rightId = ev.locations[1]?.docId ?? ev.docIds[1] ?? ev.docIds[0]
  const left = task.documents.find((d) => d.id === leftId)
  const right = task.documents.find((d) => d.id === rightId)
  if (!left || !right) return
  diffDocs.value = { left, right }
  leftIr.value = null
  rightIr.value = null
  try {
    const [l, r] = await Promise.all([
      getCompareIr(task.id, left.id),
      getCompareIr(task.id, right.id),
    ])
    leftIr.value = l
    rightIr.value = r
  } catch {
    message.warning('IR 加载失败，对比视图将不显示高亮框')
  }
  view.value = 'diff'
}

function handleSelectEvidence(ev: Evidence): void {
  const idx = evidences.value.findIndex((e) => e.id === ev.id)
  if (idx >= 0) void openDiff(idx)
}

function handleSelectPair(docA: string, docB: string): void {
  const idx = evidences.value.findIndex((e) => e.docIds.includes(docA) && e.docIds.includes(docB))
  if (idx >= 0) void openDiff(idx)
  else message.info('该文档对暂无证据')
}

function handleDiffNav(offset: number): void {
  const next = activeEvidenceIndex.value + offset
  if (next >= 0 && next < evidences.value.length) void openDiff(next)
}
```

template 中 `ResultWorkbench` 分支之后追加：

```vue
    <DiffViewer
      v-else-if="view === 'diff' && activeEvidence && diffDocs"
      :evidence="activeEvidence"
      :left-doc="diffDocs.left"
      :right-doc="diffDocs.right"
      :left-ir="leftIr"
      :right-ir="rightIr"
      :has-prev="activeEvidenceIndex > 0"
      :has-next="activeEvidenceIndex < evidences.length - 1"
      @close="view = 'result'"
      @prev="handleDiffNav(-1)"
      @next="handleDiffNav(1)"
    />
```

- [ ] **Step 2: 验证** — `pnpm run typecheck` 通过。手动走查：
  1. 工作台证据清单点「技术方案章节大面积雷同」→ 左右对比视图：左标书A、右标书B 各渲染 3 页 PDF，两侧自动跳到第 2 页，3 对雷同块（3.1/3.2/3.3）以同色框配对高亮，首对块闪烁后常亮。
  2. **bbox 对齐精度**：高亮框应正好套住对应文本行（mock IR 归一化 bbox 由演示 PDF 行坐标 ÷ 595×842 得出，渲染偏差应 <2px）；点 PdfViewer 工具栏放大到约 150% 再缩小回适应，高亮框始终精确套住文本（高亮层按渲染页 metrics 定位，缩放不漂移）。
  3. 点左侧某个高亮框 → 右侧配对框跳页闪烁（同色）；hover 高亮框显示类型 tag。
  4. 「逐块对齐」开启时滚动左栏，右栏按比例同步；关闭后互不影响。
  5. **虚拟滚动（可选，验证大文件能力）**：临时把 mock 中某文档 `fileUrl` 改为本地 100+ 页 PDF 路径（或 public 下放一份大 PDF），打开对比视图快速滚动——DOM 中 `.pdf-page-wrapper` 保持个位数（只渲染可视区页），滚动流畅无白屏闪烁；验证后改回。
  6. 「上一条/下一条」切换证据（报价证据跳到第 3 页报价行，A/B 报价框同色）；首条禁用「上一条」。
  7. 工作台热力图点 A×B 单元格 → 进入该文档对第一条证据的对比视图；点 A×C → 「封面与格式结构相似」（封面标题框高亮）。
  8. 标书B 参与对比时顶部出现 OCR warning alert；「返回」回到结果工作台。
- [ ] **Step 3: （可选）** `git add -A && git commit -m "feat(user-web): wire compare diff view into page state machine"`

---

## Task 14 【P2】条款确认页

**Files:**
- Create: `user-web/src/views/ai-bid/compare/components/ClauseConfirm.vue`
- Modify: `user-web/src/views/ai-bid/compare/index.vue`（移除 P1 自动锁定，改为人工确认页）

spec §7.1.3：左侧 AI 提取的条款草案（勾选/编辑/删除），右侧从条款库追加；点「确认锁定」才进入分析（spec §3.2 条款必须用户确认后锁定）。

- [ ] **Step 1: 创建 `user-web/src/views/ai-bid/compare/components/ClauseConfirm.vue`，内容如下（完整文件）**

```vue
<template>
  <div class="clause-confirm">
    <PageHeader title="确认强制性条款" description="AI 已从招标文件提取条款草案，请核对、补充后锁定；锁定后不可修改，作为本任务的条款快照">
      <template #extra>
        <a-button size="small" @click="emit('back')">
          <ArrowLeftOutlined /> 返回
        </a-button>
      </template>
    </PageHeader>

    <div class="clause-confirm__body">
      <SectionCard title="条款草案（AI 提取）" flush class="clause-confirm__draft">
        <a-skeleton v-if="draft.length === 0" :paragraph="{ rows: 3 }" class="clause-confirm__skeleton" />

        <div v-else class="clause-list">
          <div v-for="clause in localClauses" :key="clause.clauseId" class="clause-item">
            <a-checkbox
              :checked="selectedIds.has(clause.clauseId)"
              class="clause-item__check"
              @change="toggleSelect(clause.clauseId)"
            />
            <div class="clause-item__body">
              <a-input
                v-if="editingId === clause.clauseId"
                v-model:value="editingText"
                size="small"
                @press-enter="confirmEdit"
              />
              <span v-else class="clause-item__text">{{ clause.text }}</span>
              <div class="clause-item__meta">
                <a-tag>{{ clause.category }}</a-tag>
                <a-tag v-if="clause.mandatory" color="red">强制</a-tag>
                <a-tag v-else>一般</a-tag>
                <span class="clause-item__source">{{ sourceLabel(clause.source) }}</span>
              </div>
            </div>
            <div class="clause-item__ops">
              <a-button v-if="editingId === clause.clauseId" type="link" size="small" @click="confirmEdit">保存</a-button>
              <a-button v-else type="link" size="small" @click="startEdit(clause)">编辑</a-button>
              <a-popconfirm title="删除该条款？" @confirm="removeClause(clause.clauseId)">
                <a-button type="link" size="small" danger>删除</a-button>
              </a-popconfirm>
            </div>
          </div>

          <div class="clause-add">
            <a-input v-model:value="newClauseText" placeholder="手动新增条款，回车添加" size="small" @press-enter="addManualClause" />
          </div>
        </div>
      </SectionCard>

      <SectionCard title="从条款库追加" flush class="clause-confirm__templates">
        <EmptyState v-if="templates.length === 0" type="no-data" title="条款库为空" />
        <div v-else class="clause-list">
          <div v-for="tpl in availableTemplates" :key="tpl.clauseId" class="clause-item">
            <div class="clause-item__body">
              <span class="clause-item__text">{{ tpl.text }}</span>
              <div class="clause-item__meta">
                <a-tag>{{ tpl.category }}</a-tag>
                <a-tag v-if="tpl.mandatory" color="red">强制</a-tag>
                <a-tag v-else>一般</a-tag>
              </div>
            </div>
            <div class="clause-item__ops">
              <a-button type="link" size="small" @click="appendTemplate(tpl)">追加</a-button>
            </div>
          </div>
        </div>
      </SectionCard>
    </div>

    <div class="clause-confirm__footer">
      <span class="clause-confirm__count">已选 {{ selectedIds.size }} 条（含强制 {{ mandatoryCount }} 条）</span>
      <a-button type="primary" :loading="locking" :disabled="selectedIds.size === 0" @click="handleLock">
        确认锁定，开始分析
      </a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { ArrowLeftOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import EmptyState from '@shared/web/components/EmptyState.vue'
import type { Clause, ClauseTemplate } from '@/types'

const props = defineProps<{
  draft: Clause[]
  templates: ClauseTemplate[]
  locking: boolean
}>()

const emit = defineEmits<{
  lock: [clauses: Clause[]]
  back: []
}>()

const localClauses = ref<Clause[]>([])
const selectedIds = ref<Set<string>>(new Set())
const editingId = ref<string | null>(null)
const editingText = ref('')
const newClauseText = ref('')
let localSeq = 1

watch(() => props.draft, (draft) => {
  localClauses.value = draft.map((c) => ({ ...c }))
  selectedIds.value = new Set(draft.map((c) => c.clauseId))
}, { immediate: true })

const availableTemplates = computed(() =>
  props.templates.filter((t) => !localClauses.value.some((c) => c.text === t.text)),
)

const mandatoryCount = computed(() =>
  localClauses.value.filter((c) => selectedIds.value.has(c.clauseId) && c.mandatory).length,
)

function sourceLabel(source: Clause['source']): string {
  return source === 'extracted' ? 'AI 提取' : source === 'template' ? '条款库' : '手动新增'
}

function toggleSelect(clauseId: string): void {
  const next = new Set(selectedIds.value)
  if (next.has(clauseId)) next.delete(clauseId)
  else next.add(clauseId)
  selectedIds.value = next
}

function startEdit(clause: Clause): void {
  editingId.value = clause.clauseId
  editingText.value = clause.text
}

function confirmEdit(): void {
  const clause = localClauses.value.find((c) => c.clauseId === editingId.value)
  if (clause && editingText.value.trim()) clause.text = editingText.value.trim()
  editingId.value = null
}

function removeClause(clauseId: string): void {
  localClauses.value = localClauses.value.filter((c) => c.clauseId !== clauseId)
  const next = new Set(selectedIds.value)
  next.delete(clauseId)
  selectedIds.value = next
}

function addManualClause(): void {
  const text = newClauseText.value.trim()
  if (!text) return
  const clause: Clause = { clauseId: `manual-${localSeq++}`, source: 'manual', text, mandatory: true, category: '手动条款' }
  localClauses.value.push(clause)
  selectedIds.value = new Set([...selectedIds.value, clause.clauseId])
  newClauseText.value = ''
}

function appendTemplate(tpl: ClauseTemplate): void {
  const clause: Clause = { clauseId: `tpl-${tpl.clauseId}-${localSeq++}`, source: 'template', text: tpl.text, mandatory: tpl.mandatory, category: tpl.category }
  localClauses.value.push(clause)
  selectedIds.value = new Set([...selectedIds.value, clause.clauseId])
}

function handleLock(): void {
  emit('lock', localClauses.value.filter((c) => selectedIds.value.has(c.clauseId)))
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.clause-confirm__body {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 360px;
  gap: @spacing-xl;
  margin-bottom: @spacing-xl;
}

.clause-confirm__skeleton { padding: @spacing-md @spacing-xl; }

.clause-list {
  display: flex;
  flex-direction: column;
  gap: @spacing-sm;
  padding-top: @spacing-md;
}

.clause-item {
  display: flex;
  align-items: flex-start;
  gap: @spacing-sm;
  padding: @spacing-md;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  background: @card-bg;
}

.clause-item__check { margin-top: 2px; }
.clause-item__body { flex: 1; min-width: 0; }
.clause-item__text {
  font-size: @font-size-sm;
  color: @text-primary;
  line-height: 1.6;
}
.clause-item__meta {
  display: flex;
  align-items: center;
  gap: @spacing-xs;
  margin-top: @spacing-xs;
}
.clause-item__source { font-size: @font-size-xs; color: @text-tertiary; }
.clause-item__ops {
  display: flex;
  align-items: center;
  white-space: nowrap;
}

.clause-add { padding: @spacing-xs 0; }

.clause-confirm__footer {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: @spacing-base;
}
.clause-confirm__count {
  font-size: @font-size-sm;
  color: @text-secondary;
}
</style>
```

- [ ] **Step 2: 修改 `user-web/src/views/ai-bid/compare/index.vue`**

script import 区追加：

```ts
import ClauseConfirm from './components/ClauseConfirm.vue'
import { getClauseTemplates } from '@/api/modules/compare'
import type { Clause, ClauseTemplate } from '@/types'
```

删除 P1 占位逻辑：`let clauseAutoLocking = false` 一行，以及 `startPolling` 中整个 P1 自动锁定 if 块（含注释，共 7 行：`// P1 占位流程：有招标文件时自动提取并锁定条款草案（spec §3.2 要求用户确认后锁定，`、`// Task 14（P2）将替换为条款确认页人工锁定）`、`if (task.status === 'parsed' && task.tenderDocId && !clauseAutoLocking) {`、`clauseAutoLocking = true`、`const draft = await extractCompareClauses(task.id)`、`await lockCompareClauses(task.id, draft)`、`}`），替换为：

```ts
      // 有招标文件：停在 parsed，转条款确认页人工锁定（spec §3.2）
      if (task.status === 'parsed' && task.tenderDocId && view.value === 'progress') {
        stopPolling()
        await enterClauses()
        return
      }
```

`startPolling` 之后追加以下代码（完整代码）：

```ts
// ─── 条款确认（P2，人工锁定） ───
const clauseDraft = ref<Clause[]>([])
const clauseTemplates = ref<ClauseTemplate[]>([])
const clauseLocking = ref(false)

async function enterClauses(): Promise<void> {
  if (!currentTask.value) return
  try {
    const [draft, tpls] = await Promise.all([
      extractCompareClauses(currentTask.value.id),
      getClauseTemplates(),
    ])
    clauseDraft.value = draft
    clauseTemplates.value = tpls.items
    view.value = 'clauses'
  } catch {
    message.error('加载条款草案失败')
  }
}

async function handleLockClauses(clauses: Clause[]): Promise<void> {
  if (!currentTask.value) return
  clauseLocking.value = true
  try {
    await lockCompareClauses(currentTask.value.id, clauses)
    message.success('条款已锁定，开始查重分析')
    view.value = 'progress'
    startPolling()
  } catch {
    message.error('条款锁定失败')
  } finally {
    clauseLocking.value = false
  }
}
```

`openTask` 中 `void loadPreview(currentTask.value)` 一行之前插入：

```ts
  if (status === 'parsed' && currentTask.value.tenderDocId) {
    await enterClauses()
    return
  }
```

template 中 `AnalysisProgress` 分支之后、`ResultWorkbench` 分支之前插入：

```vue
    <ClauseConfirm
      v-else-if="view === 'clauses'"
      :draft="clauseDraft"
      :templates="clauseTemplates"
      :locking="clauseLocking"
      @lock="handleLockClauses"
      @back="backToList"
    />
```

同时删除 `AnalysisProgress` 组件中的「条款确认」alert 不再需要的说明：**保留**（parsed 状态现在会立即转 clauses 视图，该 alert 实际不再出现；保留无害，不改动组件）。

- [ ] **Step 3: 验证** — `pnpm run typecheck` 通过。手动走查：
  1. 创建任务（1 份招标文件 + 2 份标书）→ 解析完成后自动进入条款确认页：左侧 4 条 AI 提取草案全部勾选，右侧条款库 4 条模板。
  2. 编辑第一条文本、删除第二条、手动输入新条款回车添加、从条款库点「追加」→ 底部计数实时变化。
  3. 「确认锁定，开始分析」→ toast 提示后进入进度页，查重→AI 分析推进，证据出现。
  4. 再创建无招标文件的任务 → 不经过条款页，解析完直接查重。
- [ ] **Step 4: （可选）** `git add -A && git commit -m "feat(user-web): compare clause confirmation page"`

---

## Task 15 【P2】条款响应矩阵 + 指标比选表

**Files:**
- Create: `user-web/src/views/ai-bid/compare/components/ClauseMatrix.vue`
- Create: `user-web/src/views/ai-bid/compare/components/IndicatorTable.vue`
- Modify: `user-web/src/views/ai-bid/compare/components/ResultWorkbench.vue`（证据清单/条款响应矩阵/指标比选表收入 a-tabs）

spec §7.1.5：条款响应矩阵 行=强制性条款、列=标书、单元格=响应/部分响应/未响应 tag，点单元格看 AI 判定理由；指标比选表 行=关键指标、列=标书。

- [ ] **Step 1: 创建 `user-web/src/views/ai-bid/compare/components/ClauseMatrix.vue`，内容如下（完整文件）**

```vue
<template>
  <div class="clause-matrix">
    <EmptyState v-if="clauses.length === 0" type="no-data" title="本任务无条款快照" description="创建任务时上传招标文件并确认条款后，此处展示响应矩阵" />
    <a-table
      v-else
      size="small"
      :data-source="rows"
      :columns="columns"
      :pagination="false"
      row-key="clauseId"
      :scroll="{ x: 900 }"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'text'">
          <div class="clause-matrix__clause">
            <span>{{ record.text }}</span>
            <a-tag v-if="record.mandatory" color="red" class="clause-matrix__mandatory">强制</a-tag>
          </div>
        </template>
        <template v-else-if="responseOf(record.clauseId, column.key as string)">
          <a-popover trigger="click" placement="top">
            <template #title>AI 判定理由</template>
            <template #content>
              <div class="clause-matrix__reason">
                <p>{{ responseOf(record.clauseId, column.key as string)!.reason }}</p>
                <p v-if="responseOf(record.clauseId, column.key as string)!.blockIds.length > 0" class="clause-matrix__blocks">
                  原文定位：{{ responseOf(record.clauseId, column.key as string)!.blockIds.join('、') }}
                </p>
              </div>
            </template>
            <a-tag class="clause-matrix__cell" :color="statusColor(responseOf(record.clauseId, column.key as string)!.status)">
              {{ statusLabel(responseOf(record.clauseId, column.key as string)!.status) }}
            </a-tag>
          </a-popover>
        </template>
        <template v-else>—</template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import EmptyState from '@shared/web/components/EmptyState.vue'
import type { Clause, ClauseResponse, ClauseResponseStatus, CompareDocument } from '@/types'

const props = defineProps<{
  clauses: Clause[]
  responses: ClauseResponse[]
  documents: CompareDocument[]
}>()

const bids = computed(() => props.documents.filter((d) => d.role === 'bid'))

const rows = computed(() => props.clauses.map((c) => ({ ...c })))

const columns = computed(() => [
  { title: '强制性条款', dataIndex: 'text', key: 'text' },
  ...bids.value.map((d) => ({ title: d.shortName, key: d.id, width: 120 })),
])

function responseOf(clauseId: string, docId: string): ClauseResponse | undefined {
  return props.responses.find((r) => r.clauseId === clauseId && r.docId === docId)
}

function statusLabel(status: ClauseResponseStatus): string {
  return status === 'compliant' ? '响应' : status === 'partial' ? '部分响应' : '未响应'
}

/** 响应状态色（AGENTS.md §2.1：成功 green / 中风险 #F59E0B / 失败 red） */
function statusColor(status: ClauseResponseStatus): string {
  return status === 'compliant' ? 'green' : status === 'partial' ? '#F59E0B' : 'red'
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.clause-matrix__clause {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  text-align: left;
}
.clause-matrix__mandatory { flex-shrink: 0; }
.clause-matrix__cell { cursor: pointer; }
.clause-matrix__reason {
  max-width: 320px;
  p { margin-bottom: @spacing-sm; font-size: @font-size-sm; }
}
.clause-matrix__blocks {
  color: @text-tertiary;
  font-size: @font-size-xs;
}
</style>
```

- [ ] **Step 2: 创建 `user-web/src/views/ai-bid/compare/components/IndicatorTable.vue`，内容如下（完整文件）**

```vue
<template>
  <div class="indicator-table">
    <EmptyState v-if="rows.length === 0" type="no-data" title="暂无指标数据" description="AI 分析完成后展示关键指标比选" />
    <a-table
      v-else
      size="small"
      :data-source="rows"
      :columns="columns"
      :pagination="false"
      row-key="indicator"
      :scroll="{ x: 900 }"
    />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import EmptyState from '@shared/web/components/EmptyState.vue'
import type { CompareDocument, IndicatorRow } from '@/types'

const props = defineProps<{
  rows: IndicatorRow[]
  documents: CompareDocument[]
}>()

const bids = computed(() => props.documents.filter((d) => d.role === 'bid'))

const columns = computed(() => [
  { title: '指标', dataIndex: 'indicator', key: 'indicator', width: 160 },
  ...bids.value.map((d) => ({ title: d.shortName, dataIndex: d.id, key: d.id })),
])

const rows = computed(() =>
  props.rows.map((row) => {
    const record: Record<string, string> = { indicator: row.indicator }
    for (const v of row.values) record[v.docId] = v.summary
    return record
  }),
)
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.indicator-table {
  :deep(.ant-table-cell) { font-size: @font-size-sm; }
}
</style>
```

- [ ] **Step 3: 修改 `user-web/src/views/ai-bid/compare/components/ResultWorkbench.vue`**

script import 区追加：

```ts
import ClauseMatrix from './ClauseMatrix.vue'
import IndicatorTable from './IndicatorTable.vue'
```

template 中 `<EvidenceTable ... />` 整个元素替换为：

```vue
    <SectionCard class="result-workbench__tabs" nopad>
      <a-tabs v-model:active-key="activeTab" class="result-workbench__tabs-inner">
        <a-tab-pane key="evidence" tab="证据清单">
          <EvidenceTable
            :evidences="evidences"
            :documents="task.documents"
            :loading="loading"
            @select="emit('selectEvidence', $event)"
          />
        </a-tab-pane>
        <a-tab-pane key="clause" tab="条款响应矩阵">
          <div class="result-workbench__pane">
            <ClauseMatrix
              :clauses="task.clauseSnapshot"
              :responses="report?.clauseResponses ?? []"
              :documents="task.documents"
            />
          </div>
        </a-tab-pane>
        <a-tab-pane key="indicator" tab="指标比选表">
          <div class="result-workbench__pane">
            <IndicatorTable :rows="report?.indicatorRows ?? []" :documents="task.documents" />
          </div>
        </a-tab-pane>
      </a-tabs>
    </SectionCard>
```

script 中 `const props = ...` 之前追加：

```ts
import { ref } from 'vue'
```

`const { chartTheme } = useChartTheme()` 之前追加：

```ts
const activeTab = ref<'evidence' | 'clause' | 'indicator'>('evidence')
```

style 块末尾追加（AGENTS §2.14 tabs 覆盖）：

```less
.result-workbench__tabs-inner {
  padding: 0 @spacing-xl @spacing-xl;
  :deep(.ant-tabs-nav) { margin-bottom: @spacing-sm; }
  :deep(.ant-tabs-tab) { padding: 6px 10px; }
}
.result-workbench__pane { padding-top: @spacing-sm; }
```

注：EvidenceTable 自身带 SectionCard，嵌在 tab pane 内会有双层卡片边距；在 `result-workbench__pane` 无额外处理，`证据清单` pane 直接渲染 EvidenceTable（其 SectionCard 提供边框），视觉可接受；如需完全贴平可后续将 EvidenceTable 的 SectionCard 去掉，本计划不展开。

- [ ] **Step 4: 验证** — `pnpm run typecheck` 通过。手动走查：
  1. 「智慧航道疏浚工程比标」工作台 → 下方 tabs：默认「证据清单」与 P1 一致。
  2. 切「条款响应矩阵」：5 行条款 × 3 列标书；cl-3 × 标书C 为红色「未响应」，点该单元格弹出 AI 判定理由（含原文定位 doc-c:2:3）；cl-2 × 标书B 为橙色「部分响应」。
  3. 切「指标比选表」：5 行指标 × 3 列，报价/工期/质保期数值与 mock 一致。
  4. 「锚地疏浚维护比标」（partial）→ 条款矩阵与指标表显示 EmptyState 空态（mock 报告对该任务返回的是兜底结构，条款快照为空）。
- [ ] **Step 5: （可选）** `git add -A && git commit -m "feat(user-web): clause response matrix and indicator tables"`

---

## Task 16 【P2】报告导出（异步 + 轮询）

**Files:**
- Modify: `user-web/src/views/ai-bid/compare/components/ResultWorkbench.vue`（PageHeader extra 加导出下拉）
- Modify: `user-web/src/views/ai-bid/compare/components/TaskList.vue`（操作列加导出）
- Modify: `user-web/src/views/ai-bid/compare/index.vue`（导出 handler + 轮询）

spec §6.2/§9：导出异步化，前端轮询句柄状态获取下载链接；导出失败可重试。

- [ ] **Step 1: 修改 `user-web/src/views/ai-bid/compare/components/ResultWorkbench.vue`**

template 中 PageHeader 的 `#extra` 内容（现为「返回列表」按钮）替换为：

```vue
      <template #extra>
        <a-dropdown>
          <a-button type="primary" :loading="exporting">
            导出报告 <DownOutlined />
          </a-button>
          <template #overlay>
            <a-menu @click="(e: { key: string }) => emit('export', e.key as 'pdf' | 'word')">
              <a-menu-item key="pdf">导出 PDF</a-menu-item>
              <a-menu-item key="word">导出 Word</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
        <a-button size="small" @click="emit('back')">
          <ArrowLeftOutlined /> 返回列表
        </a-button>
      </template>
```

script 中 import 区 `ArrowLeftOutlined` 改为：

```ts
import { ArrowLeftOutlined, DownOutlined } from '@ant-design/icons-vue'
```

props 定义追加 `exporting`（完整 props 定义替换为）：

```ts
const props = defineProps<{
  task: CompareTaskDetail
  evidences: Evidence[]
  matrix: SimilarityMatrix | null
  report: CompareReport | null
  loading: boolean
  exporting: boolean
}>()
```

emits 定义追加 `export`（完整 emits 定义替换为）：

```ts
const emit = defineEmits<{
  selectEvidence: [evidence: Evidence]
  selectPair: [docA: string, docB: string]
  back: []
  export: [format: 'pdf' | 'word']
}>()
```

- [ ] **Step 2: 整体替换 `user-web/src/views/ai-bid/compare/components/TaskList.vue` 为以下内容（完整文件；相对 P1 版仅操作列新增「导出」按钮与 `exportTask` 事件）**

```vue
<template>
  <div class="task-list">
    <PageHeader title="比标任务" description="上传 2~5 份标书，自动完成查重、条款校验与指标比选">
      <template #extra>
        <a-button type="primary" @click="emit('create')">
          <PlusOutlined /> 创建任务
        </a-button>
      </template>
    </PageHeader>

    <SectionCard nopad>
      <a-skeleton v-if="loading" :paragraph="{ rows: 5 }" class="task-list__skeleton" />

      <EmptyState v-else-if="tasks.length === 0" type="no-data" title="暂无比标任务" description="创建任务并上传标书，开始对比分析">
        <template #action>
          <a-button type="primary" @click="emit('create')">创建任务</a-button>
        </template>
      </EmptyState>

      <a-table
        v-else
        size="small"
        :data-source="tasks"
        :columns="columns"
        :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 条` }"
        row-key="id"
        :scroll="{ x: 900 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'docCount'">{{ record.docIds.length }} 份</template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="TASK_STATUS_COLORS[record.status as CompareTaskStatus]">
              {{ TASK_STATUS_LABELS[record.status as CompareTaskStatus] }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'highRiskCount'">
            <span :class="{ 'task-list__high-risk': (record.highRiskCount ?? 0) > 0 }">
              {{ record.highRiskCount ?? 0 }}
            </span>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-button type="link" size="small" @click="emit('view', record)">查看</a-button>
            <a-button
              v-if="record.status === 'done' || record.status === 'partial'"
              type="link"
              size="small"
              @click="emit('exportTask', record)"
            >导出</a-button>
            <a-popconfirm title="确认删除该任务？" @confirm="emit('remove', record)">
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import EmptyState from '@shared/web/components/EmptyState.vue'
import type { CompareTask, CompareTaskStatus } from '@/types'
import { TASK_STATUS_COLORS, TASK_STATUS_LABELS } from '../constants'

defineProps<{ tasks: CompareTask[], loading: boolean }>()

const emit = defineEmits<{
  create: []
  view: [task: CompareTask]
  remove: [task: CompareTask]
  exportTask: [task: CompareTask]
}>()

const columns = [
  { title: '任务名', dataIndex: 'name', key: 'name' },
  { title: '标书份数', key: 'docCount', width: 100 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 120 },
  { title: '高风险数', dataIndex: 'highRiskCount', key: 'highRiskCount', width: 100 },
  { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', width: 180 },
  { title: '操作', key: 'action', width: 180 },
]
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.task-list__skeleton { padding: @spacing-xl; }

.task-list__high-risk {
  color: @danger;
  font-weight: @font-weight-semibold;
}
</style>
```

- [ ] **Step 3: 修改 `user-web/src/views/ai-bid/compare/index.vue`**

script import 区追加：

```ts
import { exportCompareReport, getCompareExportStatus } from '@/api/modules/compare'
```

`backToList` 之后追加以下代码（完整代码）：

```ts
// ─── 报告导出（异步句柄 + 轮询，spec §6.2/§9 失败可重试） ───
const exporting = ref(false)

async function pollExport(taskId: string, exportId: string): Promise<void> {
  const timer = setInterval(async () => {
    try {
      const status = await getCompareExportStatus(taskId, exportId)
      if (status.status === 'done') {
        clearInterval(timer)
        exporting.value = false
        message.success({ content: '报告已生成', key: 'compare-export' })
        if (status.downloadUrl) window.open(status.downloadUrl, '_blank')
      } else if (status.status === 'failed') {
        clearInterval(timer)
        exporting.value = false
        message.error({ content: '导出失败，可重试', key: 'compare-export' })
      }
    } catch {
      clearInterval(timer)
      exporting.value = false
      message.error({ content: '导出状态查询失败', key: 'compare-export' })
    }
  }, 1500)
}

async function handleExport(format: 'pdf' | 'word'): Promise<void> {
  if (!currentTask.value || exporting.value) return
  exporting.value = true
  message.loading({ content: '报告生成中…', key: 'compare-export', duration: 0 })
  try {
    const job = await exportCompareReport(currentTask.value.id, format)
    await pollExport(currentTask.value.id, job.exportId)
  } catch {
    exporting.value = false
    message.error({ content: '导出请求失败', key: 'compare-export' })
  }
}

async function handleExportTask(task: CompareTask): Promise<void> {
  try {
    currentTask.value = await getCompareTask(task.id)
    await handleExport('pdf')
  } catch {
    message.error('导出请求失败')
  }
}
```

template 中 `ResultWorkbench` 分支追加两个绑定（完整分支替换为）：

```vue
    <ResultWorkbench
      v-else-if="view === 'result' && currentTask"
      :task="currentTask"
      :evidences="evidences"
      :matrix="matrix"
      :report="report"
      :loading="resultLoading"
      :exporting="exporting"
      @select-evidence="handleSelectEvidence"
      @select-pair="handleSelectPair"
      @back="backToList"
      @export="handleExport"
    />
```

template 中 `TaskList` 分支追加 `@export-task`（完整分支替换为）：

```vue
    <TaskList
      v-if="view === 'list'"
      :tasks="tasks"
      :loading="listLoading"
      @create="view = 'create'"
      @view="openTask"
      @remove="handleRemove"
      @export-task="handleExportTask"
    />
```

- [ ] **Step 4: 验证** — `pnpm run typecheck` 通过。手动走查：
  1. 工作台右上「导出报告 → 导出 PDF」→ 按钮 loading +「报告生成中…」toast → 约 2.5s 后「报告已生成」并新窗口打开演示 PDF。
  2. 列表「智慧航道疏浚工程比标」行点「导出」→ 同样流程；analyzing 状态任务行无「导出」按钮。
  3. 导出中重复点击被 `exporting` 抑制。
- [ ] **Step 5: （可选）** `git add -A && git commit -m "feat(user-web): compare report export with polling"`

---

## 自查清单（计划落笔后核对）

1. **AGENTS §2.0 九步覆盖**：① 类型（Task 1）→ ② urls（Task 2）→ ③ mock data（Task 3）→ ④ api 模块（Task 4）→ ⑤ mock routes（Task 5 Step 1）→ ⑥ 注册（Task 5 Step 2/3：MOCK_MODULES + modules 数组）→ ⑦ 路由（Task 6 Step 1：manifests 已占位、确认不变）→ ⑧ 页面（Task 6~16，含 Task 11/12 的共享组件移植）→ ⑨ 每个 Task 末尾 `pnpm run typecheck`。顺序正确、无遗漏。
2. **spec §7 页面/区块映射**：见文首映射表，7.1.1~7.1.6 与结果工作台四区块、导出、§9 降级提示均有对应 Task。
3. **命名一致性**：类型（`Evidence.locations[].blockIds`、`CompareTaskDetail.documents`、`SimilarityMatrix.values`、`ExportJob.exportId`）、API 函数（`getCompareEvidences`/`openDiff` 等调用处与 Task 4 定义一致）、组件 props/emits（`TaskCreatePayload`、`LinkedHighlight`、`mapIrBlocksToHighlights`、`selectEvidence`/`selectPair`/`exportTask`、`enterResult`）跨 Task 逐一对齐；`compareDocPool`/`compareIrMap` 在 Task 3 定义、Task 5 消费；`LinkedHighlight`/`normalizeRectFromPayload` 在 Task 11 定义、Task 12/13 消费。
4. **无占位符**：全文无 TBD/TODO/「类似上一个任务」；所有 Vue SFC template/script/style 三段完整；移植文件 `PdfViewer.vue`（1800+ 行）按「复制 + 精确修改清单」指令执行（源行号/锚点齐全），不要求照抄进计划；标注「P1 占位流程」的自动锁定在 Task 14 有明确替换步骤。
5. **验证命令**：每个 Task 验证 = 仓库根 `pnpm run typecheck` + `pnpm dev` 手动走查清单（具体到点击路径与预期现象）。
