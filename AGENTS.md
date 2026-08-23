# 项目开发规范

## 1. 技术栈与约束

- TS 严格模式；Vue 组件统一 `<script setup lang="ts">`。
- 别名：`@/` → `src/`，`@shared/` → `packages/shared/src/`；跨包统一走 `@shared/`，禁止 `../../packages` 相对路径。
- 样式：LESS（变量在 `@shared/web/styles/variables.less`），组件库 ant-design-vue，图标 @ant-design/icons-vue；间距/色值/字号必须用变量，禁止硬编码。
- 主题：`useThemeStore`（key `DREDGE_AI_THEME`，light/dark/auto）；组件只用 CSS 变量或映射 Less 变量，禁止硬编码色值，具体色值见 `themes.less`；antd 经 `useTheme` 桥接。admin-web 无顶部 header/面包屑，sider 底部收起+退出，主题切换在品牌行。

## 2. 前端

### 2.1 新模块清单

新模块按顺序：类型 `shared/core/types/<m>.ts` → URL key `shared/core/api/urls.ts` → 共享 mock 数据 → API `src/api/modules/<m>.ts`（导出纯函数）→ 页面 mock 注册 → `MOCK_MODULES` + `mock/index.ts` → 两端 manifest 路由 → `views/<m>/index.vue` → `pnpm run typecheck`。

组件禁止直接 `import request`，API 必须经 `src/api/modules/<m>.ts` 封装。

### 2.2 布局与间距

紧凑布局，不依赖 antd 默认间距。关键值：页面 24（`@page-padding`）、同级区块 24（`@spacing-xl`）、PageHeader 距内容 12（`@spacing-md`）、表格上筛选栏距表格 16（`@spacing-base`）、弹框 body 16–20。参考样板 admin `views/data/static/standards/index.vue`。

颜色：状态标签 green/blue/red；风险可在 `<a-tag>` 用色值；业务代码禁裸 hex，用 `@danger/@success/@accent` 等变量，透明度用 `color-mix`。

### 2.3 表格

- 管理端列表统一 `@shared/web` 的 `DataTable`（内置 size small / pageSize 15 / showTotal / row-key / 操作列 fixed right / 列宽拖拽 / 配置筛选栏），**禁止手写 a-table + 拖拽/筛选/自适应**；弹框内表格 pageSize 10。
- 必设 `row-key`、`:loading`；无数据用 empty；多列加 `:scroll="{ x }"`。
- 固定列（序号/日期/状态/操作）设 `width`，文本列自适应；操作列宽 180。
- 筛选栏放表格卡片上方，不加背景/边框，flex + 8px gap + 16px 下间距；工具栏按钮放 `#toolbarExtra`。

### 2.4 动效

业务系统动效克制：0.6s + 缓动，只动画 transform/opacity，多元素交错。3D/WebGL/落地页式设计系统等创意 skill 不主动引入。

### 2.5 图表

ECharts 样式先读 `chart-conventions.md`。

### 2.6 复用清单（禁止重复造轮子）

写任何页面/组件前，先查这些现成物，找不到再自己写：

- 前端文档/PDF：`@angineer/docs-ui`（`PDF_Viewer` / `PDFParsedViewerCombo` / `DocViewer` + `@angineer/docs-ui/style`）；项目已有封装 `@shared/web/components/StandardPdfViewer.vue`。
- AI 对话：`@angineer/aichat-ui`（`AIChat` + `@angineer/aichat-ui/style`）；参考 user-web `StandardChat.vue`。
- 文件上传条目：`@shared/web/components/UploadFileRow.vue`（上传中/成功/失败/重试/删除行）+ `UploadFileItem` 类型（`@shared/core/types/upload`）+ `formatFileSize`（`@shared/web/utils/format`）；比标/读标等“文件列表式上传”一律复用，禁止各自造一套。
- 内部共享组件：`@shared/web/components/`（PageHeader / SectionCard / DataTable / MetricCard / ChartContainer / AppButton / DataSkeleton / EmptyState / ErrorBoundary / Logo / ThemeToggle 等）。

前端 PDF、聊天、通用 UI 一律复用上面的组件，禁止各自造一套。

### 2.7 弹框宽度

简单表单 440 / 详情 520 / 大表单 640 / 大列表 800。

### 2.8 组件模式

- `index.vue` 唯一持状态，子组件只收 props/emit；子组件禁止直接 import API。
- 每个数据域覆盖 loading / empty / error 三态。
- CSS 用 BEM；覆盖 antd 用 `:deep()`；动效提供 `prefers-reduced-motion` 降级；禁止混用 tab/space。

### 2.9 UI 零件尺寸

- 按钮统一 `AppButton`（禁裸 `a-button`）：表格/卡片 `link` `sm`；PageHeader 次要 `sm`、主要 `primary`；弹框 footer 默认 md；主 CTA `lg`。
- 输入/选择/搜索表单内默认尺寸；筛选栏搜索框宽 240。
- `a-segmented` 默认 middle 仅视图切换；`a-radio-group`/`a-switch`/日期选择器 `size="small"`；tabs 覆盖 `:deep(.ant-tabs-nav){margin-bottom:@spacing-sm}`、tab padding `6px 10px`。

## 3. 架构定位（ABP vs Python）

- **ABP（.NET 8，`backend/DredgeAI.BidCompare`，:44361）是唯一业务后端**：认证/权限（OpenIddict）、应用清单、业务编排、任务队列/后台作业、存储抽象、PostgreSQL、对外 HTTP API。凡涉及“业务状态、权限、持久化、给前端的 API”都放 ABP；ABP 不直接跑模型/算法。
- **Python services 是算法/推理基础设施**（内部 HTTP，无业务持久化，由 ABP 调用）：
  - `ai-gateway`（:8200）：平台唯一 LLM 入口（OpenAI 兼容 chat / SSE），消费 `angineer-ai-inference`；
  - `compare-algo`（:8100）：确定性比标算法（similarity / pricing / metadata），不碰 LLM；
  - `meeting-bot`（:8101）：AI 晨会模型推理（ASR / TTS / 人脸 / 计数）。
- **Angineer 生态（复用/依赖，不重复实现）**：`docs-ui`（前端文档展示）、`aichat-ui`（前端对话）、`docs-api`（仓库外解析服务 :8790，ABP 经 `HttpAnGineerClient` 调用）、`ai-inference`（LLM 推理库，ai-gateway 消费）。
- 规则：LLM/算法/模型推理走 Python 服务；业务编排/权限/持久化/API 走 ABP。

## 4. 后端

- 接口设计先读 `abp-api-conventions.md`。
- .NET 工具链：用 `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe`（SDK 8.0.423，含 ASP.NET Core 8.0.29）；PATH 上的 `C:\Program Files\dotnet` 是空壳，直接敲 `dotnet` 会报 No SDK/frameworks；Docker 无 .NET SDK 镜像。
  构建：`& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/DredgeAI.BidCompare.HttpApi.Host.csproj -c Debug`
  启动：设 `$env:DOTNET_ROOT="$env:LOCALAPPDATA\Microsoft\dotnet"` 后启动 `bin\Debug\net8.0\...HttpApi.Host.exe`；判活看 44361 监听；日志 `data/logs/backend.log`、`Logs/logs.txt`。
