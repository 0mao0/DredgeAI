# Admin-web 标准规范模块：批量操作与上传文档设计

## 背景

admin-web「知识库 → 标准规范」页（`admin-web/src/views/data/static/standards.vue`）当前支持：

- 列表筛选、分页、查看 PDF 原文
- 单条 AI 解析（弹窗展示 summary / keyPoints / riskWarnings）
- 单条删除、编辑

本次新增：

1. 批量删除、批量解析
2. 上传文档：批量选择 PDF → AI 预读自动填写元数据 → 点击上传后弹窗收起、任务在后台继续，完成时页面提示并刷新列表

标准规范模块当前整体走前端 mock。本次仍以 mock 实现，但 API 层按真实后端接口形状定义，后续接真实后端时只替换 API / mock 层，UI 与交互不变。

## 范围

### In

- 表格行选择 + 批量解析进度弹窗 + 批量删除
- 上传文档弹窗（多选 PDF、AI 预填折叠条、可编辑元数据）
- 后台上传任务抽屉（逐文件进度、失败重试）
- 共享类型、URL、API 模块、mock 路由扩展

### Out

- 真实后端接口（LLM 调用、PDF 解析、文件存储、数据库）本次不做，仅预留 API 形状
- user-web 标准查询不受影响
- PDF 原文高亮解析（bbox）不纳入本次上传链路，仅保留现有演示行为

## 交互流程

### 上传文档

1. PageHeader `#extra` 新增主按钮「上传文档」（`type="primary"`）。
2. 点击打开 `StandardUploadModal`（宽度 800px）：支持拖拽/点击多选 `.pdf`，单个 ≤ 50MB，单批 ≤ 10 个，不满足直接拒绝并提示。
3. 每个文件生成一条折叠条：
   - 收起态：文件名 + AI 预读进度（0→100%）+ 状态（待上传 / 预读中 / 已就绪 / 预读失败）。
   - 展开态：元数据表单（名称、编号、行业、性质、级别、状态、发布部门、发布年份、简介），AI 预读完成后自动填写，用户可编辑；每条可单独移除。
4. 点击底部「上传」：校验必填项（名称、编号），弹窗收起，任务进入后台队列。
5. PageHeader 右侧出现「上传任务」入口并带数量 Badge；点击打开 `StandardUploadTasksDrawer`，复用折叠条展示逐文件上传进度（上传中 / 已完成 / 失败）。
6. 全部完成或全部失败时 `message` 汇总提示；成功则自动刷新列表；失败行显示原因并提供重试。

### 批量解析

1. 表格增加行选择（`row-key="id"`）。
2. 选中后出现操作栏：显示「已选 N 条」+「批量解析」「批量删除」按钮（未选中时禁用，按钮 `size="small"`）。
3. 批量解析打开 `StandardBatchParseModal`（宽度 640px）：逐条显示「解析中 / 成功 / 失败」；成功行可点「查看」打开原有 AI 解析详情弹窗；失败行显示原因并可单独重试；结束后汇总提示并刷新列表「已解析」状态。

### 批量删除

点击「批量删除」弹出确认「确定删除选中的 N 条标准？」，确认后调用批量删除接口；失败时保留勾选并提示，成功刷新列表。

## 架构与组件

- 页面调整为目录结构：`admin-web/src/views/data/static/standards/index.vue` + `components/`，路由 manifest 导入路径同步更新。
- `index.vue` 是唯一状态持有者：列表、勾选、弹窗开关、API 调用全部集中在页面。
- 新增组件（纯 props / emits，禁止直接 `import request` 或调用 API 模块）：
  - `StandardUploadModal.vue`：选文件、AI 预填折叠条、元数据表单
  - `StandardUploadTasksDrawer.vue`：后台任务抽屉，复用折叠条展示进度与重试
  - `StandardBatchParseModal.vue`：批量解析逐条状态 + 「查看」按钮
- 新增 `composables/useStandardUpload.ts`：上传任务队列状态机（AI 预读进度、后台上传进度、失败重试），仅被 `index.vue` 使用。

## 数据层

### 共享类型 `packages/shared/src/core/types/standard.ts`

- `StandardPropertyInput`：`{ name, code, industry?, nature?, level?, status?, issuer?, publishYear?, description?, parentId? }`
- `StandardParseBatchResult`：`{ id, success, analysis?, error? }`

### URL `packages/shared/src/core/api/urls.ts`

- `adminStandardCreate: '/standards'`（POST，创建记录）
- `adminStandardPreview: '/standards/preview'`（POST，AI 预读单文件）
- `adminStandardsBatchDelete: '/standards/batch-delete'`（POST，body `{ ids }`）
- `adminStandardsBatchParse: '/standards/batch-parse'`（POST，body `{ ids }`）

### API `admin-web/src/api/modules/standards.ts`

- `previewStandard(file: File): Promise<StandardPropertyInput>`（multipart）
- `uploadStandard(file: File, data: StandardPropertyInput): Promise<StandardProperty>`（multipart，含文件与元数据 JSON）
- `deleteStandards(ids: string[]): Promise<number>`（批量删除，返回成功数量）
- `parseStandards(ids: string[]): Promise<StandardParseBatchResult[]>`（批量解析，返回逐条结果）

### Mock `admin-web/src/mock/routes/standards.ts`

- `POST /standards/preview`：按文件名确定性生成预填字段（名称取文件名、编号按 GB/T 规则伪生成、其余字段按确定性规则生成），延迟约 1.2s 模拟 AI 预读。
- `POST /standards`：解析 FormData，生成自增 id（`std-N`）写入 `adminStandards`，延迟模拟上传耗时，返回完整 `StandardProperty`。
- `POST /standards/batch-delete`：从 `adminStandards` 移除指定 id，返回成功数量。
- `POST /standards/batch-parse`：逐 id 生成或复用 `standardAIAnalyses`，返回逐条结果。
- 保持现有内存态行为（页面刷新后重置）。

## 状态与错误处理

- 文件校验：类型 `.pdf`、大小 ≤ 50MB、单批 ≤ 10；不满足立即拒绝并提示。
- AI 预读失败：折叠条状态置为「预读失败」，提供重试。
- 上传失败：抽屉中该条显示失败原因 + 重试，不中断其他任务；上传中关闭抽屉不中断任务。
- 批量解析失败：失败行可单独重试，不阻塞其他行。
- 列表沿用现有 loading / error / empty 三态处理。
- 自定义展开/进度动效补充 `prefers-reduced-motion` 降级。

## 规范遵循

- 按 AGENTS.md 新模块清单顺序：类型 → URL → mock 数据 → API → mock 路由 → 注册 → 页面 → `pnpm run typecheck`。
- 组件模式：Props Down / Events Up，API 调用集中在 `index.vue`（composable 归属页面逻辑）。
- 表格 `size="small"`、`row-key`、操作列固定 180px、分页 15 条/页；批量操作按钮 `size="small"`。
- 样式使用 Less 变量与主题 CSS 变量，BEM 命名，禁止硬编码色值。
- 每一步完成即运行 `pnpm run typecheck`，不积累类型错误。

## 验证

1. `pnpm run typecheck` 通过。
2. 运行 admin-web dev，手动验证全链路：
   - 上传：选择多个 PDF → AI 预读进度 → 展开编辑 → 点击上传 → 弹窗收起 → 后台任务进度 → 完成提示 → 列表出现新记录。
   - 批量解析：勾选多条 → 进度弹窗逐条状态 → 「查看」打开解析详情 → 列表「已解析」状态更新。
   - 批量删除：勾选多条 → 确认 → 列表刷新。
   - 失败路径：预读失败重试、上传失败重试、解析失败单条重试。
3. mock 开关关闭时不新增真实依赖，现有行为不回归。

## 后续真实后端接入点

- `previewStandard` → 后端调 AnGIneer 解析 PDF 文本 + `Llm` 配置的默认模型提取元数据。
- `uploadStandard` → 后端存储文件（开发 `App_Data/storage`，生产 S3/MinIO）并落库。
- `batch-delete` / `batch-parse` → ABP AppService 批量操作。
- 前端组件与交互保持不变，仅替换 API / mock 层。
