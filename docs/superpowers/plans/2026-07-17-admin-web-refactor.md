# admin-web 架构脚手架实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 搭建 `admin-web`（端口 5374）独立 Vite 工程，完整实现 Dashboard 管理工作台 + 4 个骨架页（permissions / applications / data / analytics），作为 adminWeb 团队接手新架构的参考范本，最后删除旧 `platform/` 目录。

**Architecture:** 与 `user-web` 平行的独立 Vite 工程，复用同一套设计 token（sky 主色 + cyan 强调 + slate 深色侧边栏）但代码不共享。axios + axios-mock-adapter 拦截请求返回静态 mock，由代码常量 `USE_MOCK` 控制。Dashboard 完整示范页面架构、组件用法、图表集成、mock 调用链路；其余 4 页用 `PageSkeleton` 占位并标注待实现模块。

**Tech Stack:** Vue 3.5 + TypeScript 5.7 + Vite 6 + Pinia 2 + Vue Router 4 + ant-design-vue 4 + UnoCSS + Less + axios 1 + axios-mock-adapter + echarts 5 + vue-echarts 7 + dayjs + @vueuse/core + nprogress

**设计 spec:** `docs/superpowers/specs/2026-07-17-dual-web-refactor-design.md`（第 8.2 节、第 7.3 节、第 6.2 节）

**前置依赖:** 根 workspace（`pnpm-workspace.yaml` / `tsconfig.base.json` / `.npmrc` / 根 `package.json`）已由 `2026-07-17-dual-web-refactor.md` Task 1 创建完成。

---

## 跨端统一约定（user-web 与 admin-web 共同遵守）

> 以下 3 条约定同时适用于 user-web 与 admin-web。user-web 计划文件（`2026-07-17-dual-web-refactor.md`）在执行前需按本章节同步修订。

### 约定 1：左侧菜单分组——个人中心 / API Keys 固定置底

两端 `Layout` 的 `a-menu` 必须分两组：

- **主菜单组**（顶部）：业务功能菜单（如 user-web 的「工作台/应用广场/AI 审标/标准查询」、admin-web 的「管理工作台/权限管理/应用管理/数据治理/分析洞察」）
- **账户菜单组**（底部，紧贴侧边栏底端）：固定两项
  - **个人中心**（`/profile`，user-web）/ **账户设置**（`/profile`，admin-web）
  - **API Keys**（`/api`，user-web） / **API Keys 管理**（`/api`，admin-web）

实现方式：用 `<a-menu-item-group>` 分组，账户组通过 CSS `margin-top: auto` 推到侧边栏底部，侧边栏 `<a-layout-sider>` 内部使用 flex 纵向布局。

### 约定 2：Dark / Light 主题切换——公共文件统一管理

两端均需支持暗色/亮色双主题，**由右上角图标切换**，主题 token **统一由公共文件管理**，禁止每个 `.less` 文件单独写暗色覆盖。

**实现架构（两端各维护一份，代码相同）：**

- `src/composables/useTheme.ts`：主题组合式函数，封装切换、持久化（localStorage key `DREDGE_AI_THEME`）、`<html data-theme="dark|light">` 属性切换、`@vueuse/core` `useColorMode` 集成
- `src/styles/themes.less`：**唯一的主题 token 文件**，用 CSS Variables 定义所有颜色 token，通过 `[data-theme="light"]` / `[data-theme="dark"]` 两套选择器分别赋值
- `src/styles/variables.less`：所有颜色变量改为 `var(--xxx)` 引用 themes.less 的 CSS Variables，字号/间距/圆角等与主题无关的 token 保持 LESS 原值
- `src/components/ThemeToggle.vue`：右上角图标组件，太阳/月亮切换，挂载在两个 Layout 的 header 右侧
- `src/stores/app.ts`：增加 `theme: 'light' | 'dark'` 状态，由 persistedstate 持久化，初始化时同步到 `<html>`

**主题 token 覆盖范围**（themes.less 必须为下列 token 提供 light/dark 两套值）：

```less
// themes.less 关键 CSS Variables（节选）
[data-theme="light"] {
  --color-bg-content: #F8FAFC;
  --color-bg-card: #FFFFFF;
  --color-bg-sidebar: #0F172A;       // 侧边栏始终深色
  --color-text-primary: #0F172A;
  --color-text-secondary: #475569;
  --color-text-tertiary: #94A3B8;
  --color-border: #E2E8F0;
  --color-divider: #F1F5F9;
  --color-brand: #0EA5E9;
  --color-brand-hover: #0284C7;
}
[data-theme="dark"] {
  --color-bg-content: #0B1220;
  --color-bg-card: #1E293B;
  --color-bg-sidebar: #020617;       // 暗色更深
  --color-text-primary: #F1F5F9;
  --color-text-secondary: #CBD5E1;
  --color-text-tertiary: #64748B;
  --color-border: #334155;
  --color-divider: #1E293B;
  --color-brand: #38BDF8;            // 暗色下提亮
  --color-brand-hover: #7DD3FC;
}
```

**ant-design-vue 4.x 暗色主题**：通过 `ConfigProvider` 的 `:theme="themeConfig"` 注入，`themeConfig` 由 `useTheme` 返回的 `algorithm`（`theme.defaultAlgorithm` / `theme.darkAlgorithm`）驱动。

### 约定 3：user-web 每个 AI 应用页面——「当前任务」+「历史记录」双区结构

user-web 的每个具体 AI 应用页面（AI 审标 / 标准查询 / 智能写作 / 合同审查 / 知识问答 等）必须包含两个功能区，让用户能检索并复用历史任一次应用的输入与结果：

- **当前任务区**（顶部/左侧，主区域）：本次交互的输入、过程、结果
- **历史记录区**（侧边/底部抽屉/Tab 切换）：列表展示该应用的历史会话，每条记录可点击「查看」恢复到当前任务区，支持搜索、按时间筛选、删除

**统一数据契约（user-web types/index.ts 增补）：**

```ts
// AI 应用会话记录（通用，所有 AI 应用页面复用）
export interface AppSession {
  id: string
  appId: string                 // 所属应用，如 'bid-review' | 'standard' | 'writing' | 'contract' | 'qa'
  title: string                 // 会话标题（取首条输入摘要）
  inputs: Record<string, unknown>   // 原始输入（文件、文本、参数等）
  result: Record<string, unknown>   // 原始结果（结构化）
  resultPreview: string         // 结果预览摘要（列表展示用）
  createdAt: string
  updatedAt: string
  durationMs?: number
  starred?: boolean
}

// 会话列表查询参数
export interface SessionQuery {
  appId: string
  keyword?: string
  startDate?: string
  endDate?: string
  starredOnly?: boolean
  page?: number
  pageSize?: number
}
```

**统一 API（user-web api/modules/session.ts）：**

```ts
// 会话相关 API（所有 AI 应用页面共用）
export function listSessions(query: SessionQuery): Promise<{ list: AppSession[]; total: number }>
export function getSession(id: string): Promise<AppSession>
export function deleteSession(id: string): Promise<void>
export function toggleStarSession(id: string): Promise<void>
```

**统一组件（user-web components/AppSessionHistory.vue）：**

- props: `appId: string`
- 内部调用 `listSessions({ appId })` 拉取历史
- 顶部搜索框 + 时间筛选 + 仅看星标开关
- 列表项：标题、时间、结果预览、星标按钮、查看按钮、删除按钮
- 点击「查看」emit `select` 事件，父页面接收后恢复到当前任务区

**user-web 各 AI 应用页面调整（修订 dual-web-refactor.md Task 11-14）：**

| 页面 | 当前任务区内容 | 历史记录区内容 |
|---|---|---|
| AI 审标 (`/bid-review`) | 上传招标文件 → 风险识别 → 对话追问 | 历史审标会话列表，可恢复某次审标的文档、风险列表、对话片段 |
| 标准查询 (`/standards`) | 输入查询 → 匹配结果列表 → 详情 | 历史查询记录，可恢复某次查询的输入与命中结果 |
| 智能写作 (`/writing`) | 输入要求 → 生成文档 → 编辑 | 历史写作记录，可恢复某次的输入与生成结果 |
| 合同审查 (`/contract`) | 上传合同 → 风险识别 → 报告 | 历史审查记录，可恢复某次的合同与审查报告 |
| 知识问答 (`/qa`) | 对话输入 → 回答 | 历史问答会话列表，可恢复某次完整对话 |

> 注：智能写作 / 合同审查 / 知识问答 3 个页面在原 dual-web-refactor.md 计划中未列入首期 6 页，若本次不实现则记为 TODO，但 `AppSession` 契约与 `AppSessionHistory.vue` 组件必须在首期落地，AI 审标与标准查询两个首期页面必须接入。

---

## 文件结构总览

### admin-web (端口 5374)
```
admin-web/
├── package.json / vite.config.ts / tsconfig.json / tsconfig.node.json
├── index.html / env.d.ts
└── src/
    ├── api/{request.ts, mock/index.ts, modules/dashboard.ts}
    ├── mock/{dashboard.ts, chart.ts}
    ├── types/index.ts
    ├── utils/{constants.ts, request.ts, format.ts}
    ├── composables/useTheme.ts          // ★ 跨端约定 2：主题切换公共组合式函数
    ├── components/{PageHeader,SectionCard,MetricCard,PageSkeleton,ChartContainer,DataSkeleton,ThemeToggle}.vue
    ├── layouts/AdminLayout.vue          // ★ 跨端约定 1：菜单分组（主菜单 + 置底账户组）
    ├── router/index.ts                  // ★ 增加 /profile /api 路由
    ├── stores/app.ts                    // ★ 增加 theme 状态
    ├── styles/{variables.less, themes.less, reset.less, global.less}  // ★ themes.less 主题 token 单一来源
    ├── uno.config.ts
    ├── App.vue / main.ts                // ★ App.vue 包 ConfigProvider 注入 antd 主题
    └── views/{dashboard,permissions,applications,data,analytics,profile,api}/index.vue  // ★ 增加 profile/api 两页
```

> user-web 文件结构同步调整：增加 `composables/useTheme.ts`、`styles/themes.less`、`components/{ThemeToggle,AppSessionHistory}.vue`、`api/modules/session.ts`、`mock/session.ts`，并在 AI 应用页面（bid-review / standards）落地区分「当前任务」与「历史记录」的双区布局。

---

## Task 1: admin-web 工程脚手架与依赖

**Files:**
- Create: `d:\AI\DredgeAI\admin-web\package.json`
- Create: `d:\AI\DredgeAI\admin-web\vite.config.ts`
- Create: `d:\AI\DredgeAI\admin-web\tsconfig.json`
- Create: `d:\AI\DredgeAI\admin-web\tsconfig.node.json`
- Create: `d:\AI\DredgeAI\admin-web\index.html`
- Create: `d:\AI\DredgeAI\admin-web\env.d.ts`
- Create: `d:\AI\DredgeAI\admin-web\src\App.vue`

- [ ] **Step 1: 创建 admin-web/package.json**

```json
{
  "name": "admin-web",
  "private": true,
  "version": "0.1.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "vue-tsc -b && vite build",
    "preview": "vite preview",
    "typecheck": "vue-tsc --noEmit"
  },
  "dependencies": {
    "@ant-design/icons-vue": "^7.0.1",
    "@vueuse/core": "^11.3.0",
    "ant-design-vue": "^4.2.6",
    "axios": "^1.7.9",
    "axios-mock-adapter": "^2.1.0",
    "dayjs": "^1.11.13",
    "echarts": "^5.6.0",
    "lodash-es": "^4.17.21",
    "nprogress": "^0.2.0",
    "pinia": "^2.2.8",
    "pinia-plugin-persistedstate": "^4.2.0",
    "vue": "^3.5.13",
    "vue-echarts": "^7.0.3",
    "vue-router": "^4.5.0"
  },
  "devDependencies": {
    "@types/lodash-es": "^4.17.12",
    "@types/nprogress": "^0.2.3",
    "@vitejs/plugin-vue": "^5.2.3",
    "less": "^4.2.2",
    "typescript": "~5.7.3",
    "unocss": "^66.1.0",
    "vite": "^6.3.1",
    "vue-tsc": "^2.2.8"
  }
}
```

- [ ] **Step 2: 创建 admin-web/vite.config.ts**

```ts
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import UnoCSS from 'unocss/vite'
import { resolve } from 'path'

// admin-web vite 配置：端口 5374
export default defineConfig({
  plugins: [vue(), UnoCSS()],
  resolve: {
    alias: { '@': resolve(__dirname, 'src') },
  },
  server: { port: 5374, host: true, open: false },
  css: {
    preprocessorOptions: {
      less: {
        javascriptEnabled: true,
        modifyVars: {
          'primary-color': '#0EA5E9',
          'link-color': '#0EA5E9',
          'border-radius-base': '8px',
          'font-size-base': '14px',
        },
      },
    },
  },
})
```

- [ ] **Step 3: 创建 admin-web/tsconfig.json**

```json
{
  "extends": "../tsconfig.base.json",
  "compilerOptions": {
    "baseUrl": ".",
    "paths": { "@/*": ["src/*"] },
    "types": ["vite/client"]
  },
  "include": ["src/**/*.ts", "src/**/*.d.ts", "src/**/*.vue", "env.d.ts"],
  "references": [{ "path": "./tsconfig.node.json" }]
}
```

- [ ] **Step 4: 创建 admin-web/tsconfig.node.json**

```json
{
  "extends": "../tsconfig.base.json",
  "compilerOptions": {
    "composite": true,
    "skipLibCheck": true,
    "module": "ESNext",
    "moduleResolution": "bundler",
    "allowSyntheticDefaultImports": true,
    "strict": false
  },
  "include": ["vite.config.ts", "uno.config.ts"]
}
```

- [ ] **Step 5: 创建 admin-web/env.d.ts**

```ts
/// <reference types="vite/client" />

declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<Record<string, never>, Record<string, never>, unknown>
  export default component
}
```

- [ ] **Step 6: 创建 admin-web/index.html**

```html
<!doctype html>
<html lang="zh-CN">
  <head>
    <meta charset="UTF-8" />
    <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>智浚 AI · 管理后台</title>
  </head>
  <body>
    <div id="app"></div>
    <script type="module" src="/src/main.ts"></script>
  </body>
</html>
```

- [ ] **Step 7: 创建 admin-web/src/App.vue**

```vue
<template>
  <router-view />
</template>

<script setup lang="ts">
// 根组件仅承载路由出口
</script>
```

- [ ] **Step 8: 安装依赖**

Run: `pnpm install`
Expected: 安装成功，无 peer dependency 报错

- [ ] **Step 9: 提交**

```bash
git add admin-web/
git commit -m "feat(admin-web): scaffold project with vite + ts + unocss"
```

---

## Task 2: admin-web 设计 token 与全局样式（含主题切换 token）

**Files:**
- Create: `d:\AI\DredgeAI\admin-web\src\styles\themes.less` ★ 主题 token 单一来源（跨端约定 2）
- Create: `d:\AI\DredgeAI\admin-web\src\styles\variables.less`
- Create: `d:\AI\DredgeAI\admin-web\src\styles\reset.less`
- Create: `d:\AI\DredgeAI\admin-web\src\styles\global.less`
- Create: `d:\AI\DredgeAI\admin-web\src\uno.config.ts`

> 与 user-web 同一套设计 token，代码独立维护（spec 第 9 节：两端各自维护一份但视觉一致）。
> 颜色 token 走 CSS Variables（在 themes.less 中按 `[data-theme]` 切换），字号/间距/圆角等与主题无关的 token 仍用 LESS 变量。

- [ ] **Step 1: 创建 themes.less（★ 主题 token 单一来源）**

```less
// 主题 token 单一来源：所有颜色 token 的 light/dark 两套值集中在此文件
// 业务样式文件禁止再写 :root[data-theme="dark"] 覆盖

[data-theme="light"] {
  // 品牌色
  --color-brand: #0EA5E9;
  --color-brand-hover: #0284C7;
  --color-brand-gradient: linear-gradient(135deg, #0EA5E9 0%, #06B6D4 100%);
  --color-accent: #06B6D4;

  // 状态色
  --color-success: #10B981;
  --color-warning: #F59E0B;
  --color-danger: #EF4444;
  --color-info: #3B82F6;

  // 中性色
  --color-sidebar-bg: #0F172A;       // 侧边栏始终深色（两套主题保持一致）
  --color-sidebar-bg-2: #1E293B;
  --color-content-bg: #F8FAFC;
  --color-card-bg: #FFFFFF;
  --color-text-primary: #0F172A;
  --color-text-secondary: #475569;
  --color-text-tertiary: #94A3B8;
  --color-border: #E2E8F0;
  --color-divider: #F1F5F9;

  // 阴影
  --shadow-sm: 0 1px 2px rgb(0 0 0 / 0.05);
  --shadow-md: 0 4px 12px rgb(15 23 42 / 0.08);
  --shadow-lg: 0 12px 32px rgb(15 23 42 / 0.12);
  --shadow-brand: 0 8px 24px rgb(14 165 233 / 0.25);

  // ant-design-vue 渲染辅助
  --antd-header-bg: #FFFFFF;
  --antd-sider-bg: #0F172A;
}

[data-theme="dark"] {
  // 品牌色（暗色下提亮）
  --color-brand: #38BDF8;
  --color-brand-hover: #7DD3FC;
  --color-brand-gradient: linear-gradient(135deg, #38BDF8 0%, #22D3EE 100%);
  --color-accent: #22D3EE;

  // 状态色
  --color-success: #34D399;
  --color-warning: #FBBF24;
  --color-danger: #F87171;
  --color-info: #60A5FA;

  // 中性色
  --color-sidebar-bg: #020617;
  --color-sidebar-bg-2: #0F172A;
  --color-content-bg: #0B1220;
  --color-card-bg: #1E293B;
  --color-text-primary: #F1F5F9;
  --color-text-secondary: #CBD5E1;
  --color-text-tertiary: #64748B;
  --color-border: #334155;
  --color-divider: #1E293B;

  // 阴影（暗色下减弱）
  --shadow-sm: 0 1px 2px rgb(0 0 0 / 0.3);
  --shadow-md: 0 4px 12px rgb(0 0 0 / 0.4);
  --shadow-lg: 0 12px 32px rgb(0 0 0 / 0.5);
  --shadow-brand: 0 8px 24px rgb(56 189 248 / 0.3);

  --antd-header-bg: #1E293B;
  --antd-sider-bg: #020617;
}

// 兜底：未设置 data-theme 时按 light 渲染
:root {
  --color-brand: #0EA5E9;
  --color-brand-hover: #0284C7;
  --color-brand-gradient: linear-gradient(135deg, #0EA5E9 0%, #06B6D4 100%);
  --color-accent: #06B6D4;
  --color-success: #10B981;
  --color-warning: #F59E0B;
  --color-danger: #EF4444;
  --color-info: #3B82F6;
  --color-sidebar-bg: #0F172A;
  --color-sidebar-bg-2: #1E293B;
  --color-content-bg: #F8FAFC;
  --color-card-bg: #FFFFFF;
  --color-text-primary: #0F172A;
  --color-text-secondary: #475569;
  --color-text-tertiary: #94A3B8;
  --color-border: #E2E8F0;
  --color-divider: #F1F5F9;
  --shadow-sm: 0 1px 2px rgb(0 0 0 / 0.05);
  --shadow-md: 0 4px 12px rgb(15 23 42 / 0.08);
  --shadow-lg: 0 12px 32px rgb(15 23 42 / 0.12);
  --shadow-brand: 0 8px 24px rgb(14 165 233 / 0.25);
  --antd-header-bg: #FFFFFF;
  --antd-sider-bg: #0F172A;
}
```

- [ ] **Step 2: 创建 variables.less（设计 token，颜色走 CSS Variables）**

```less
// 颜色 token：引用 themes.less 中的 CSS Variables，自动跟随主题切换
@brand-primary: var(--color-brand);
@brand-primary-hover: var(--color-brand-hover);
@brand-gradient: var(--color-brand-gradient);
@accent: var(--color-accent);

@success: var(--color-success);
@warning: var(--color-warning);
@danger: var(--color-danger);
@info: var(--color-info);

@sidebar-bg: var(--color-sidebar-bg);
@sidebar-bg-2: var(--color-sidebar-bg-2);
@content-bg: var(--color-content-bg);
@card-bg: var(--color-card-bg);
@text-primary: var(--color-text-primary);
@text-secondary: var(--color-text-secondary);
@text-tertiary: var(--color-text-tertiary);
@border-color: var(--color-border);
@divider-color: var(--color-divider);

// 字号（与主题无关，保持 LESS 常量）
@font-size-xs: 12px;
@font-size-sm: 13px;
@font-size-base: 14px;
@font-size-lg: 16px;
@font-size-xl: 18px;
@font-size-2xl: 22px;
@font-size-3xl: 28px;
@font-size-4xl: 36px;

// 字重
@font-weight-regular: 400;
@font-weight-medium: 500;
@font-weight-semibold: 600;
@font-weight-bold: 700;

// 字体族
@font-family: -apple-system, 'PingFang SC', 'Microsoft YaHei', 'Segoe UI', sans-serif;

// 圆角
@radius-sm: 6px;
@radius-base: 8px;
@radius-lg: 12px;
@radius-xl: 16px;

// 间距
@spacing-xs: 4px;
@spacing-sm: 8px;
@spacing-md: 12px;
@spacing-base: 16px;
@spacing-lg: 20px;
@spacing-xl: 24px;
@spacing-2xl: 32px;
@spacing-3xl: 40px;
@spacing-4xl: 48px;

// 阴影（同样走 CSS Variables，暗色下减弱）
@shadow-sm: var(--shadow-sm);
@shadow-md: var(--shadow-md);
@shadow-lg: var(--shadow-lg);
@shadow-brand: var(--shadow-brand);

// 布局
@sidebar-width: 240px;
@sidebar-collapsed-width: 64px;
@header-height: 64px;
@content-max-width: 1440px;
@page-padding: 24px;

// 动效
@transition-fast: 150ms ease;
@transition-base: 200ms ease;
@transition-slow: 300ms ease;
```

- [ ] **Step 3: 创建 reset.less**

```less
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

html, body, #app {
  height: 100%;
  width: 100%;
}

ul, ol { list-style: none; }
a { color: inherit; text-decoration: none; }
button { font-family: inherit; cursor: pointer; }
```

- [ ] **Step 4: 创建 global.less（先 import themes.less 让 CSS Variables 全局生效）**

```less
@import './themes.less';      // ★ 必须最先导入，提供 :root 与 [data-theme] 的 CSS Variables
@import './variables.less';   // 引用 CSS Variables 的 LESS 别名

body {
  font-family: @font-family;
  background-color: @content-bg;
  color: @text-primary;
  font-size: @font-size-base;
  line-height: 1.6;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  transition: background-color @transition-base, color @transition-base;
}

.page-container {
  padding: @page-padding;
  max-width: @content-max-width;
  margin: 0 auto;
}

::-webkit-scrollbar { width: 6px; height: 6px; }
::-webkit-scrollbar-track { background: transparent; }
::-webkit-scrollbar-thumb {
  background: @border-color;
  border-radius: 3px;
  &:hover { background: @text-tertiary; }
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity @transition-base, transform @transition-base;
}
.fade-enter-from { opacity: 0; transform: translateY(4px); }
.fade-leave-to { opacity: 0; }

// ant-design-vue 组件视觉与设计 token 对齐（颜色随主题切换）
.ant-card {
  border-radius: @radius-lg !important;
  border-color: @border-color !important;
  background: @card-bg !important;
}
.ant-btn-primary {
  background: @brand-primary !important;
  border-color: @brand-primary !important;
  &:hover {
    background: @brand-primary-hover !important;
    border-color: @brand-primary-hover !important;
  }
}
```

- [ ] **Step 5: 创建 uno.config.ts（颜色引用 CSS Variables，跟随主题切换）**

```ts
import { defineConfig, presetUno, presetAttributify, presetIcons } from 'unocss'

// UnoCSS 配置：颜色 token 引用 themes.less 的 CSS Variables，自动跟随主题切换
export default defineConfig({
  presets: [
    presetUno(),
    presetAttributify(),
    presetIcons({ scale: 1.2, warn: true }),
  ],
  theme: {
    colors: {
      brand: { DEFAULT: 'var(--color-brand)', hover: 'var(--color-brand-hover)' },
      accent: 'var(--color-accent)',
      success: 'var(--color-success)',
      warning: 'var(--color-warning)',
      danger: 'var(--color-danger)',
      info: 'var(--color-info)',
      sidebar: { DEFAULT: 'var(--color-sidebar-bg)', 2: 'var(--color-sidebar-bg-2)' },
      content: 'var(--color-content-bg)',
      card: 'var(--color-card-bg)',
      text: {
        primary: 'var(--color-text-primary)',
        secondary: 'var(--color-text-secondary)',
        tertiary: 'var(--color-text-tertiary)',
      },
      border: 'var(--color-border)',
      divider: 'var(--color-divider)',
    },
    boxShadow: {
      sm: 'var(--shadow-sm)',
      md: 'var(--shadow-md)',
      lg: 'var(--shadow-lg)',
      brand: 'var(--shadow-brand)',
    },
    borderRadius: {
      sm: '6px',
      base: '8px',
      lg: '12px',
      xl: '16px',
    },
  },
  shortcuts: {
    'card-hover': 'transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md',
    'flex-center': 'flex items-center justify-center',
    'flex-between': 'flex items-center justify-between',
  },
})
```

- [ ] **Step 6: 提交**

```bash
git add admin-web/src/styles/ admin-web/src/uno.config.ts
git commit -m "feat(admin-web): add theme tokens (CSS Variables) and global styles"
```

---

## Task 3: admin-web 类型定义与工具函数

**Files:**
- Create: `d:\AI\DredgeAI\admin-web\src\types\index.ts`
- Create: `d:\AI\DredgeAI\admin-web\src\utils\constants.ts`
- Create: `d:\AI\DredgeAI\admin-web\src\utils\request.ts`
- Create: `d:\AI\DredgeAI\admin-web\src\utils\format.ts`

- [ ] **Step 1: 创建 types/index.ts**

```ts
// 管理后台指标卡
export interface AdminMetric {
  id: string
  title: string
  value: string | number
  trend?: string
  trendUp?: boolean
  sparkline?: number[]
}

// 待办告警
export interface AlertItem {
  id: string
  level: '严重' | '警告' | '提示'
  title: string
  source: string
  time: string
  status: '待处理' | '处理中' | '已解决'
}

// 应用排行
export interface AppRankingItem {
  rank: number
  name: string
  calls: number
  trend: number
}

// 审核提醒
export interface ReviewReminder {
  id: string
  type: '应用上架' | '权限申请' | 'Key 申请' | '配额调整'
  applicant: string
  content: string
  time: string
}

// 图表数据
export interface ChartSeries { name: string; data: number[] }
export interface LineChartData { categories: string[]; series: ChartSeries[] }
export interface BarChartData { categories: string[]; series: ChartSeries[] }
export interface PieChartData { name: string; data: { name: string; value: number }[] }
```

- [ ] **Step 2: 创建 utils/constants.ts**

```ts
// 认证令牌 localStorage key
export const STORAGE_TOKEN_KEY = 'DREDGE_AI_ADMIN_TOKEN'

// 跨端跳转地址（user-web）
export const USER_WEB_URL = 'http://localhost:5373'
```

- [ ] **Step 3: 创建 utils/request.ts**

```ts
// 后端统一响应结构
export interface ApiResponse<T = unknown> {
  code: number
  data: T
  message: string
}

// 模拟网络延迟
export function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

// 随机延迟 200-400ms
export function randomDelay(): Promise<void> {
  return delay(200 + Math.floor(Math.random() * 200))
}
```

- [ ] **Step 4: 创建 utils/format.ts**

```ts
import dayjs from 'dayjs'
import relativeTime from 'dayjs/plugin/relativeTime'
import 'dayjs/locale/zh-cn'

dayjs.extend(relativeTime)
dayjs.locale('zh-cn')

// 格式化日期
export function formatDate(date: string | Date, format = 'YYYY-MM-DD HH:mm'): string {
  return dayjs(date).format(format)
}

// 相对时间
export function fromNow(date: string | Date): string {
  return dayjs(date).fromNow()
}

// 千分位格式化数字
export function formatNumber(num: number): string {
  return num.toLocaleString('zh-CN')
}
```

- [ ] **Step 5: 提交**

```bash
git add admin-web/src/types/ admin-web/src/utils/
git commit -m "feat(admin-web): add type definitions and utils"
```

---

## Task 4: admin-web Mock 数据

**Files:**
- Create: `d:\AI\DredgeAI\admin-web\src\mock\dashboard.ts`
- Create: `d:\AI\DredgeAI\admin-web\src\mock\chart.ts`

- [ ] **Step 1: 创建 mock/dashboard.ts**

```ts
import type { AdminMetric, AlertItem, AppRankingItem, ReviewReminder } from '@/types'

// 顶部 4 个指标卡
export const adminMetrics: AdminMetric[] = [
  { id: 'm-1', title: '今日调用量', value: '128,560', trend: '12.5%', trendUp: true, sparkline: [820, 932, 901, 1290, 1330, 1320, 1450] },
  { id: 'm-2', title: '活跃用户', value: '1,892', trend: '8.2%', trendUp: true, sparkline: [120, 132, 101, 134, 90, 230, 210] },
  { id: 'm-3', title: '异常告警', value: 7, trend: '3.1%', trendUp: false, sparkline: [5, 3, 4, 6, 8, 7, 7] },
  { id: 'm-4', title: '待审核', value: 23, trend: '15.8%', trendUp: false, sparkline: [18, 20, 19, 22, 21, 23, 23] },
]

// 待办告警列表
export const alertItems: AlertItem[] = [
  { id: 'a-1', level: '严重', title: '生产环境 API Key 调用量超配额 120%', source: 'GPT-4o · 生产环境', time: '2026-07-17 14:32', status: '待处理' },
  { id: 'a-2', level: '严重', title: '本地模型节点离线超过 10 分钟', source: '本地模型集群', time: '2026-07-17 14:15', status: '处理中' },
  { id: 'a-3', level: '警告', title: 'Claude 3.5 Sonnet 响应延迟 P99 > 8s', source: 'Claude 3.5 Sonnet', time: '2026-07-17 13:48', status: '待处理' },
  { id: 'a-4', level: '警告', title: '测试环境 Key 异常高频调用', source: '测试环境', time: '2026-07-17 12:20', status: '已解决' },
  { id: 'a-5', level: '提示', title: '通义千问-Max 模型已升级到 v2', source: '通义千问-Max', time: '2026-07-17 10:05', status: '已解决' },
  { id: 'a-6', level: '提示', title: '本周 API 调用报告已生成', source: '系统报告', time: '2026-07-17 09:00', status: '已解决' },
]

// 应用排行 TOP5
export const appRanking: AppRankingItem[] = [
  { rank: 1, name: 'AI 审标', calls: 45820, trend: 18.5 },
  { rank: 2, name: '智能写作', calls: 32140, trend: 12.3 },
  { rank: 3, name: '知识问答', calls: 28690, trend: -2.1 },
  { rank: 4, name: '合同审查', calls: 19530, trend: 8.7 },
  { rank: 5, name: '标准查询', calls: 14280, trend: 5.4 },
]

// 审核提醒列表
export const reviewReminders: ReviewReminder[] = [
  { id: 'r-1', type: '应用上架', applicant: '李伟（研发部）', content: '申请上架「文档比对」应用', time: '2026-07-17 11:20' },
  { id: 'r-2', type: '权限申请', applicant: '王芳（市场部）', content: '申请「数据看板」查看权限', time: '2026-07-17 10:45' },
  { id: 'r-3', type: 'Key 申请', applicant: '陈强（集成商）', content: '申请新增第三方集成 API Key', time: '2026-07-17 09:30' },
  { id: 'r-4', type: '配额调整', applicant: '张明（工程技术部）', content: '申请将 AI 审标专用 Key 配额提升至 50000', time: '2026-07-16 17:15' },
  { id: 'r-5', type: '应用上架', applicant: '赵敏（产品部）', content: '申请上架「会议纪要」v1.2', time: '2026-07-16 14:50' },
]
```

- [ ] **Step 2: 创建 mock/chart.ts**

```ts
import type { LineChartData, BarChartData, PieChartData } from '@/types'

// 调用趋势折线图（近 7 天）
export const callTrend: LineChartData = {
  categories: ['7/11', '7/12', '7/13', '7/14', '7/15', '7/16', '7/17'],
  series: [
    { name: '总调用', data: [95000, 102000, 88000, 115000, 128000, 121000, 128560] },
    { name: '成功调用', data: [92800, 100200, 86500, 112800, 125800, 119200, 126500] },
  ],
}

// 应用排行柱状图
export const appRankingBar: BarChartData = {
  categories: appRankingCategories(),
  series: [{ name: '调用次数', data: [45820, 32140, 28690, 19530, 14280] }],
}

// 模型成本分布饼图
export const modelCostPie: PieChartData = {
  name: '模型成本分布',
  data: [
    { name: 'GPT-4o', value: 18200 },
    { name: 'Claude 3.5 Sonnet', value: 12300 },
    { name: '通义千问-Max', value: 4800 },
    { name: '本地模型', value: 3200 },
    { name: 'DeepSeek-V3', value: 1500 },
  ],
}

// 应用排行类目（供 BarChart 复用）
function appRankingCategories(): string[] {
  return ['AI 审标', '智能写作', '知识问答', '合同审查', '标准查询']
}
```

- [ ] **Step 3: 提交**

```bash
git add admin-web/src/mock/
git commit -m "feat(admin-web): add mock data for dashboard"
```

---

## Task 5: admin-web API 层（axios + mock-adapter）

**Files:**
- Create: `d:\AI\DredgeAI\admin-web\src\api\request.ts`
- Create: `d:\AI\DredgeAI\admin-web\src\api\mock\index.ts`
- Create: `d:\AI\DredgeAI\admin-web\src\api\mock\modules\dashboard.ts`
- Create: `d:\AI\DredgeAI\admin-web\src\api\modules\dashboard.ts`
- Create: `d:\AI\DredgeAI\admin-web\src\main.ts`

- [ ] **Step 1: 创建 api/request.ts（axios 实例 + 拦截器）**

```ts
import axios from 'axios'
import type { AxiosResponse } from 'axios'
import nprogress from 'nprogress'
import 'nprogress/nprogress.css'
import type { ApiResponse } from '@/utils/request'
import { STORAGE_TOKEN_KEY } from '@/utils/constants'

// 创建 axios 实例
const request = axios.create({
  baseURL: '/api',
  timeout: 15000,
})

// 请求拦截器：附加 token + 启动进度条
request.interceptors.request.use((config) => {
  nprogress.start()
  const token = localStorage.getItem(STORAGE_TOKEN_KEY)
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// 响应拦截器：解包 data + 关闭进度条
request.interceptors.response.use(
  (response: AxiosResponse<ApiResponse>) => {
    nprogress.done()
    const res = response.data
    if (res.code !== 0) {
      return Promise.reject(new Error(res.message || '请求失败'))
    }
    return res.data as unknown as AxiosResponse
  },
  (error) => {
    nprogress.done()
    return Promise.reject(error)
  },
)

export default request
```

- [ ] **Step 2: 创建 api/mock/index.ts（mock-adapter 注册中心）**

```ts
import MockAdapter from 'axios-mock-adapter'
import request from '@/api/request'
import { randomDelay } from '@/utils/request'
import { registerDashboardMock } from './modules/dashboard'

// mock 总开关：未来切真实接口时改为 false
const USE_MOCK = true

// 注册全部 mock 拦截
export function registerMock(): void {
  if (!USE_MOCK) return

  const mock = new MockAdapter(request, { delayResponse: 0 })

  // 包装 onGet 等方法，统一加入随机延迟
  const wrap = (handler: () => unknown) => async () => {
    await randomDelay()
    return [200, { code: 0, data: handler(), message: 'ok' }]
  }

  registerDashboardMock(mock, wrap)
}
```

- [ ] **Step 3: 创建 api/mock/modules/dashboard.ts**

```ts
import type MockAdapter from 'axios-mock-adapter'
import { adminMetrics, alertItems, appRanking, reviewReminders } from '@/mock/dashboard'
import { callTrend, appRankingBar, modelCostPie } from '@/mock/chart'

// 注册工作台相关 mock
export function registerDashboardMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/dashboard/metrics').reply(wrap(() => adminMetrics))
  mock.onGet('/dashboard/alerts').reply(wrap(() => alertItems))
  mock.onGet('/dashboard/app-ranking').reply(wrap(() => appRanking))
  mock.onGet('/dashboard/review-reminders').reply(wrap(() => reviewReminders))
  mock.onGet('/dashboard/call-trend').reply(wrap(() => callTrend))
  mock.onGet('/dashboard/app-ranking-chart').reply(wrap(() => appRankingBar))
  mock.onGet('/dashboard/model-cost').reply(wrap(() => modelCostPie))
}
```

- [ ] **Step 4: 创建 api/modules/dashboard.ts**

```ts
import request from '@/api/request'
import type { AdminMetric, AlertItem, AppRankingItem, ReviewReminder, LineChartData, BarChartData, PieChartData } from '@/types'

// 获取顶部指标卡
export function getDashboardMetrics(): Promise<AdminMetric[]> {
  return request.get('/dashboard/metrics') as unknown as Promise<AdminMetric[]>
}

// 获取待办告警
export function getAlerts(): Promise<AlertItem[]> {
  return request.get('/dashboard/alerts') as unknown as Promise<AlertItem[]>
}

// 获取应用排行
export function getAppRanking(): Promise<AppRankingItem[]> {
  return request.get('/dashboard/app-ranking') as unknown as Promise<AppRankingItem[]>
}

// 获取审核提醒
export function getReviewReminders(): Promise<ReviewReminder[]> {
  return request.get('/dashboard/review-reminders') as unknown as Promise<ReviewReminder[]>
}

// 获取调用趋势折线图
export function getCallTrend(): Promise<LineChartData> {
  return request.get('/dashboard/call-trend') as unknown as Promise<LineChartData>
}

// 获取应用排行柱状图
export function getAppRankingChart(): Promise<BarChartData> {
  return request.get('/dashboard/app-ranking-chart') as unknown as Promise<BarChartData>
}

// 获取模型成本饼图
export function getModelCost(): Promise<PieChartData> {
  return request.get('/dashboard/model-cost') as unknown as Promise<PieChartData>
}
```

- [ ] **Step 5: 创建 main.ts（注册 mock + 挂载 app）**

```ts
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import piniaPluginPersistedstate from 'pinia-plugin-persistedstate'
import Antd from 'ant-design-vue'
import 'ant-design-vue/dist/reset.css'
import 'virtual:uno.css'
import App from './App.vue'
import router from './router'
import './styles/reset.less'
import './styles/global.less'
import { registerMock } from './api/mock'

// 注册 mock 拦截器（仅在 mock 模式下生效）
registerMock()

const app = createApp(App)
const pinia = createPinia()
pinia.use(piniaPluginPersistedstate)

app.use(pinia)
app.use(router)
app.use(Antd)
app.mount('#app')
```

- [ ] **Step 6: 提交**

```bash
git add admin-web/src/api/ admin-web/src/main.ts
git commit -m "feat(admin-web): setup axios + mock-adapter with dashboard API"
```

---

## Task 6: admin-web Pinia store

**Files:**
- Create: `d:\AI\DredgeAI\admin-web\src\stores\app.ts`

- [ ] **Step 1: 创建 stores/app.ts**

```ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { AdminMetric, AlertItem, AppRankingItem, ReviewReminder } from '@/types'
import {
  getDashboardMetrics, getAlerts, getAppRanking, getReviewReminders,
} from '@/api/modules/dashboard'

// 管理后台全局状态：工作台数据 + 侧边栏折叠
export const useAppStore = defineStore('admin-app', () => {
  const metrics = ref<AdminMetric[]>([])
  const alerts = ref<AlertItem[]>([])
  const appRanking = ref<AppRankingItem[]>([])
  const reviewReminders = ref<ReviewReminder[]>([])
  const sidebarCollapsed = ref(false)

  async function fetchMetrics(): Promise<void> { metrics.value = await getDashboardMetrics() }
  async function fetchAlerts(): Promise<void> { alerts.value = await getAlerts() }
  async function fetchAppRanking(): Promise<void> { appRanking.value = await getAppRanking() }
  async function fetchReviewReminders(): Promise<void> { reviewReminders.value = await getReviewReminders() }

  function toggleSidebar(): void { sidebarCollapsed.value = !sidebarCollapsed.value }

  // 拉取工作台全部数据
  async function fetchDashboard(): Promise<void> {
    await Promise.all([fetchMetrics(), fetchAlerts(), fetchAppRanking(), fetchReviewReminders()])
  }

  return {
    metrics, alerts, appRanking, reviewReminders, sidebarCollapsed,
    fetchMetrics, fetchAlerts, fetchAppRanking, fetchReviewReminders, fetchDashboard, toggleSidebar,
  }
}, {
  persist: { pick: ['sidebarCollapsed'] },
})
```

- [ ] **Step 2: 提交**

```bash
git add admin-web/src/stores/
git commit -m "feat(admin-web): add pinia store with persistedstate"
```

---

## Task 7: admin-web 通用组件

**Files:**
- Create: `d:\AI\DredgeAI\admin-web\src\components\PageHeader.vue`
- Create: `d:\AI\DredgeAI\admin-web\src\components\SectionCard.vue`
- Create: `d:\AI\DredgeAI\admin-web\src\components\MetricCard.vue`
- Create: `d:\AI\DredgeAI\admin-web\src\components\PageSkeleton.vue`
- Create: `d:\AI\DredgeAI\admin-web\src\components\ChartContainer.vue`
- Create: `d:\AI\DredgeAI\admin-web\src\components\DataSkeleton.vue`

- [ ] **Step 1: 创建 PageHeader.vue**

```vue
<template>
  <div class="page-header">
    <div class="page-header-left">
      <h2 class="page-title">{{ title }}</h2>
      <p v-if="description" class="page-desc">{{ description }}</p>
    </div>
    <div v-if="$slots.extra" class="page-header-right">
      <slot name="extra" />
    </div>
  </div>
</template>

<script setup lang="ts">
// 页面标题组件：标题 + 描述 + 右侧操作区
defineProps<{ title: string; description?: string }>()
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.page-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: @spacing-xl;
}
.page-title {
  font-size: @font-size-2xl;
  font-weight: @font-weight-semibold;
  color: @text-primary;
  line-height: 1.3;
}
.page-desc {
  font-size: @font-size-sm;
  color: @text-secondary;
  margin-top: @spacing-xs;
}
</style>
```

- [ ] **Step 2: 创建 SectionCard.vue**

```vue
<template>
  <div class="section-card">
    <div v-if="title || $slots.title || $slots.extra" class="section-card-header">
      <div class="section-card-title">
        <slot name="title">{{ title }}</slot>
      </div>
      <div v-if="$slots.extra" class="section-card-extra">
        <slot name="extra" />
      </div>
    </div>
    <div class="section-card-body" :class="{ 'section-card-body--nopad': nopad }">
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
// 区块卡片：标题、操作区、内容区
withDefaults(defineProps<{ title?: string; nopad?: boolean }>(), { nopad: false })
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.section-card {
  background: @card-bg;
  border-radius: @radius-lg;
  border: 1px solid @border-color;
  box-shadow: @shadow-sm;
  transition: box-shadow @transition-base;
  &:hover { box-shadow: @shadow-md; }
}
.section-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: @spacing-lg @spacing-xl;
  border-bottom: 1px solid @divider-color;
}
.section-card-title {
  font-size: @font-size-lg;
  font-weight: @font-weight-semibold;
  color: @text-primary;
}
.section-card-body {
  padding: @spacing-xl;
  &--nopad { padding: 0; }
}
</style>
```

- [ ] **Step 3: 创建 MetricCard.vue（带 sparkline mini 折线）**

```vue
<template>
  <div class="metric-card">
    <div class="metric-header">
      <span class="metric-label">{{ title }}</span>
      <div v-if="trend" class="metric-trend" :class="trendUp ? 'up' : 'down'">
        <component :is="trendUp ? ArrowUpOutlined : ArrowDownOutlined" />
        <span>{{ trend }}</span>
      </div>
    </div>
    <div class="metric-value">{{ value }}</div>
    <div v-if="sparkline && sparkline.length" class="metric-spark">
      <v-chart :option="sparkOption" autoresize />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { ArrowUpOutlined, ArrowDownOutlined } from '@ant-design/icons-vue'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart } from 'echarts/charts'
import { TooltipComponent, GridComponent } from 'echarts/components'
import VChart from 'vue-echarts'

// 注册 echarts 组件
use([CanvasRenderer, LineChart, TooltipComponent, GridComponent])

// 指标卡：标题、数值、趋势、mini sparkline
const props = defineProps<{
  title: string
  value: string | number
  trend?: string
  trendUp?: boolean
  sparkline?: number[]
}>()

const sparkOption = computed(() => ({
  grid: { left: 0, right: 0, top: 4, bottom: 0 },
  xAxis: { type: 'category', show: false, boundaryGap: false, data: props.sparkline?.map((_, i) => i) || [] },
  yAxis: { type: 'value', show: false, scale: true },
  series: [{
    type: 'line',
    data: props.sparkline || [],
    smooth: true,
    symbol: 'none',
    lineStyle: { width: 2, color: props.trendUp ? '#10B981' : '#EF4444' },
    areaStyle: { opacity: 0.15 },
  }],
}))
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.metric-card {
  background: @card-bg;
  border-radius: @radius-lg;
  border: 1px solid @border-color;
  padding: @spacing-xl;
  box-shadow: @shadow-sm;
  transition: all @transition-base;
  &:hover { box-shadow: @shadow-md; transform: translateY(-2px); }
}
.metric-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: @spacing-sm;
}
.metric-label {
  font-size: @font-size-sm;
  color: @text-secondary;
}
.metric-value {
  font-size: @font-size-3xl;
  font-weight: @font-weight-bold;
  color: @text-primary;
  line-height: 1;
}
.metric-trend {
  display: flex;
  align-items: center;
  gap: 2px;
  font-size: @font-size-xs;
  &.up { color: @success; }
  &.down { color: @danger; }
}
.metric-spark {
  height: 40px;
  margin-top: @spacing-sm;
}
</style>
```

- [ ] **Step 4: 创建 PageSkeleton.vue（骨架占位页统一组件）**

```vue
<template>
  <div class="page-container">
    <PageHeader :title="title" :description="description" />
    <a-alert
      type="info"
      show-icon
      message="本页由 adminWeb 团队基于此架构实现，参考 dashboard 页与 design token 体系"
      class="skeleton-alert"
    />
    <SectionCard title="示意结构">
      <a-empty :description="`待 adminWeb 团队实现：${implementHint}`">
        <template #image>
          <a-avatar :size="64" class="skeleton-avatar">
            <template #icon><BuildOutlined /></template>
          </a-avatar>
        </template>
      </a-empty>
      <div class="skeleton-hint">
        <h4>架构参考清单</h4>
        <ul>
          <li>页面入口：`views/{{ routeName }}/index.vue`（当前文件）</li>
          <li>数据获取：参考 `api/modules/dashboard.ts` 创建对应模块</li>
          <li>Mock 数据：参考 `mock/dashboard.ts` 与 `api/mock/modules/dashboard.ts` 注册路由</li>
          <li>状态管理：参考 `stores/app.ts` 创建对应 store</li>
          <li>UI 组件：复用 `components/` 下 PageHeader / SectionCard / ChartContainer 等</li>
          <li>设计 token：见 `styles/variables.less`，颜色/字号/间距/阴影统一</li>
        </ul>
      </div>
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { BuildOutlined } from '@ant-design/icons-vue'
import PageHeader from './PageHeader.vue'
import SectionCard from './SectionCard.vue'

// 骨架占位页：标注待实现模块，提供架构参考
defineProps<{
  title: string
  description: string
  routeName: string
  implementHint: string
}>()
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.skeleton-alert {
  margin-bottom: @spacing-xl;
}
.skeleton-avatar {
  background: @brand-gradient;
}
.skeleton-hint {
  margin-top: @spacing-xl;
  padding-top: @spacing-lg;
  border-top: 1px solid @divider-color;
  h4 {
    font-size: @font-size-base;
    font-weight: @font-weight-semibold;
    color: @text-primary;
    margin-bottom: @spacing-md;
  }
  ul {
    padding-left: @spacing-xl;
    li {
      font-size: @font-size-sm;
      color: @text-secondary;
      line-height: 1.8;
    }
  }
}
</style>
```

- [ ] **Step 5: 创建 ChartContainer.vue**

```vue
<template>
  <div class="chart-container" :style="{ height }">
    <a-spin v-if="loading" class="chart-spin" />
    <v-chart v-else :option="option" autoresize class="chart" />
  </div>
</template>

<script setup lang="ts">
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart, BarChart, PieChart } from 'echarts/charts'
import {
  TitleComponent, TooltipComponent, LegendComponent,
  GridComponent, DataZoomComponent,
} from 'echarts/components'
import VChart from 'vue-echarts'

// 注册 echarts 组件
use([
  CanvasRenderer, LineChart, BarChart, PieChart,
  TitleComponent, TooltipComponent, LegendComponent,
  GridComponent, DataZoomComponent,
])

// echarts 容器：自适应、loading
defineProps<{
  option: Record<string, unknown>
  height?: string
  loading?: boolean
}>()
</script>

<style scoped lang="less">
.chart-container {
  width: 100%;
  position: relative;
}
.chart-spin {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
}
.chart {
  width: 100%;
  height: 100%;
}
</style>
```

- [ ] **Step 6: 创建 DataSkeleton.vue**

```vue
<template>
  <div class="data-skeleton">
    <a-skeleton v-for="i in rows" :key="i" :title="{ width: '60%' }" :paragraph="{ rows: 2 }" active />
  </div>
</template>

<script setup lang="ts">
// 数据加载骨架屏
withDefaults(defineProps<{ rows?: number }>(), { rows: 3 })
</script>

<style scoped lang="less">
.data-skeleton > * + * {
  margin-top: 16px;
}
</style>
```

- [ ] **Step 7: 创建 ThemeToggle.vue（★ 跨端约定 2：右上角主题切换图标）**

```vue
<template>
  <a-tooltip :title="isDark ? '切换到亮色' : '切换到暗色'">
    <component
      :is="isDark ? SunOutlined : MoonOutlined"
      class="theme-toggle"
      @click="toggle"
    />
  </a-tooltip>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { SunOutlined, MoonOutlined } from '@ant-design/icons-vue'
import { useAppStore } from '@/stores/app'

// 主题切换按钮：太阳/月亮图标，点击切换 dark/light
const appStore = useAppStore()
const isDark = computed(() => appStore.theme === 'dark')

function toggle(): void {
  appStore.setTheme(isDark.value ? 'light' : 'dark')
}
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.theme-toggle {
  font-size: 18px;
  color: @text-secondary;
  cursor: pointer;
  transition: color @transition-fast;
  &:hover { color: @brand-primary; }
}
</style>
```

- [ ] **Step 8: 提交**

```bash
git add admin-web/src/components/
git commit -m "feat(admin-web): add shared UI components including PageSkeleton and ThemeToggle"
```

---

## Task 7.5: admin-web 主题切换基础设施（★ 跨端约定 2）

**Files:**
- Create: `d:\AI\DredgeAI\admin-web\src\composables\useTheme.ts`
- Modify: `d:\AI\DredgeAI\admin-web\src\stores\app.ts`（Task 6 创建的 store 增补 theme 字段）
- Modify: `d:\AI\DredgeAI\admin-web\src\App.vue`（包 ConfigProvider 注入 antd 主题）
- Modify: `d:\AI\DredgeAI\admin-web\src\main.ts`（启动时同步 `<html data-theme>`）

- [ ] **Step 1: 创建 composables/useTheme.ts**

```ts
import { computed } from 'vue'
import { theme as antdTheme } from 'ant-design-vue'
import { useAppStore } from '@/stores/app'

// 主题组合式函数：统一对外暴露当前主题、切换方法、antd algorithm
export function useTheme() {
  const appStore = useAppStore()
  const isDark = computed(() => appStore.theme === 'dark')

  // ant-design-vue 4.x algorithm：暗色用 darkAlgorithm，亮色用 defaultAlgorithm
  const algorithm = computed(() =>
    isDark.value ? antdTheme.darkAlgorithm : antdTheme.defaultAlgorithm,
  )

  // antd ConfigProvider 的 theme 配置
  const themeConfig = computed(() => ({
    algorithm: algorithm.value,
    token: {
      colorPrimary: isDark.value ? '#38BDF8' : '#0EA5E9',
      borderRadius: 8,
      fontSize: 14,
    },
  }))

  function toggle(): void {
    appStore.setTheme(isDark.value ? 'light' : 'dark')
  }

  function setTheme(t: 'light' | 'dark'): void {
    appStore.setTheme(t)
  }

  return { isDark, algorithm, themeConfig, toggle, setTheme }
}
```

- [ ] **Step 2: 修改 stores/app.ts 增补 theme 字段**

在 Task 6 创建的 `useAppStore` 中追加：

```ts
import { ref, watch } from 'vue'

// 在 defineStore 工厂函数内追加：
const theme = ref<'light' | 'dark'>(localStorage.getItem('DREDGE_AI_THEME') === 'dark' ? 'dark' : 'light')

function setTheme(t: 'light' | 'dark'): void {
  theme.value = t
  localStorage.setItem('DREDGE_AI_THEME', t)
  document.documentElement.setAttribute('data-theme', t)
}

// 初始化时同步一次 <html data-theme>
if (typeof document !== 'undefined') {
  document.documentElement.setAttribute('data-theme', theme.value)
}

// 暴露到 return
return {
  // ... 原有字段
  theme, setTheme,
  // ...
}
```

> 注意：theme 不通过 persistedstate 持久化（已手动写入 localStorage），避免 SSR 时序问题。`persist.pick` 不包含 `theme`。

- [ ] **Step 3: 修改 App.vue 包 ConfigProvider**

```vue
<template>
  <a-config-provider :theme="themeConfig">
    <router-view />
  </a-config-provider>
</template>

<script setup lang="ts">
import { useTheme } from '@/composables/useTheme'

// 根组件：注入 antd 主题 algorithm，驱动 ant-design-vue 4.x 暗色/亮色渲染
const { themeConfig } = useTheme()
</script>
```

- [ ] **Step 4: 提交**

```bash
git add admin-web/src/composables/ admin-web/src/stores/app.ts admin-web/src/App.vue
git commit -m "feat(admin-web): add theme toggle infrastructure (useTheme + ConfigProvider)"
```

---

## Task 8: admin-web 布局与路由（★ 跨端约定 1：菜单分组置底）

**Files:**
- Create: `d:\AI\DredgeAI\admin-web\src\layouts\AdminLayout.vue`
- Create: `d:\AI\DredgeAI\admin-web\src\router\index.ts`

- [ ] **Step 1: 创建 AdminLayout.vue（slate 深色侧边栏 + 菜单分组置底 + 头部含 ThemeToggle）**

```vue
<template>
  <a-layout class="admin-layout">
    <a-layout-sider
      v-model:collapsed="collapsed"
      :trigger="null"
      collapsible
      theme="dark"
      :width="240"
      :collapsed-width="64"
      class="sider"
    >
      <div class="logo">
        <div class="logo-mark">管</div>
        <div v-if="!collapsed" class="logo-text">
          <div class="logo-name">智浚 AI</div>
          <div class="logo-sub">Admin Console</div>
        </div>
      </div>
      <!-- ★ 跨端约定 1：菜单分两组，主菜单组在顶部，账户组（个人中心/API Keys）固定置底 -->
      <a-menu
        v-model:selectedKeys="selectedKeys"
        theme="dark"
        mode="inline"
        class="sider-menu"
        @click="handleMenuClick"
      >
        <a-menu-item-group key="main" class="menu-group-main">
          <a-menu-item key="/dashboard">
            <DashboardOutlined />
            <span>管理工作台</span>
          </a-menu-item>
          <a-menu-item key="/permissions">
            <SafetyOutlined />
            <span>权限管理</span>
          </a-menu-item>
          <a-menu-item key="/applications">
            <AppstoreOutlined />
            <span>应用管理</span>
          </a-menu-item>
          <a-menu-item key="/data">
            <DatabaseOutlined />
            <span>数据治理</span>
          </a-menu-item>
          <a-menu-item key="/analytics">
            <BarChartOutlined />
            <span>分析洞察</span>
          </a-menu-item>
        </a-menu-item-group>
        <a-menu-item-group key="account" class="menu-group-account">
          <a-menu-item key="/profile">
            <UserOutlined />
            <span>账户设置</span>
          </a-menu-item>
          <a-menu-item key="/api">
            <KeyOutlined />
            <span>API Keys 管理</span>
          </a-menu-item>
        </a-menu-item-group>
      </a-menu>
    </a-layout-sider>

    <a-layout class="main-layout">
      <a-layout-header class="header">
        <div class="header-left">
          <component
            :is="collapsed ? MenuUnfoldOutlined : MenuFoldOutlined"
            class="trigger"
            @click="collapsed = !collapsed"
          />
          <a-input-search
            placeholder="搜索用户、应用、Key..."
            class="header-search"
          />
        </div>
        <div class="header-right">
          <!-- ★ 跨端约定 2：右上角主题切换图标 -->
          <ThemeToggle />
          <a-badge :count="pendingAlertCount" :offset="[2, -2]">
            <BellOutlined class="header-icon" />
          </a-badge>
          <a-tooltip title="用户端">
            <UserSwitchOutlined class="header-icon" @click="goUser" />
          </a-tooltip>
          <a-dropdown>
            <span class="user-info">
              <a-avatar class="user-avatar">A</a-avatar>
              <span class="user-name">管理员</span>
            </span>
            <template #overlay>
              <a-menu @click="handleUserMenu">
                <a-menu-item key="logout"><LogoutOutlined /> 退出登录</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
        </div>
      </a-layout-header>

      <a-layout-content class="content">
        <router-view v-slot="{ Component }">
          <transition name="fade" mode="out-in">
            <component :is="Component" />
          </transition>
        </router-view>
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  DashboardOutlined, SafetyOutlined, AppstoreOutlined,
  DatabaseOutlined, BarChartOutlined, MenuFoldOutlined, MenuUnfoldOutlined,
  BellOutlined, UserSwitchOutlined, LogoutOutlined, UserOutlined, KeyOutlined,
} from '@ant-design/icons-vue'
import { useAppStore } from '@/stores/app'
import { USER_WEB_URL, STORAGE_TOKEN_KEY } from '@/utils/constants'
import ThemeToggle from '@/components/ThemeToggle.vue'

const router = useRouter()
const route = useRoute()
const appStore = useAppStore()

const collapsed = ref(appStore.sidebarCollapsed)
const selectedKeys = ref<string[]>([route.path])

// 待处理告警数
const pendingAlertCount = computed(() => appStore.alerts.filter((a) => a.status === '待处理').length)

onMounted(() => {
  if (appStore.alerts.length === 0) {
    appStore.fetchAlerts()
  }
})

watch(() => route.path, (p) => { selectedKeys.value = [p] })
watch(collapsed, (v) => { appStore.sidebarCollapsed = v })

function handleMenuClick({ key }: { key: string }): void {
  router.push(key)
}

function goUser(): void {
  window.location.href = USER_WEB_URL
}

function handleUserMenu({ key }: { key: string }): void {
  if (key === 'logout') {
    localStorage.removeItem(STORAGE_TOKEN_KEY)
    router.push('/dashboard')
  }
}
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.admin-layout { height: 100vh; }

.sider {
  background: @sidebar-bg !important;
  :deep(.ant-layout-sider-children) {
    display: flex;
    flex-direction: column;
    height: 100%;
  }
}

.logo {
  height: @header-height;
  display: flex;
  align-items: center;
  gap: @spacing-md;
  padding: 0 @spacing-xl;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  flex-shrink: 0;
}
.logo-mark {
  width: 32px;
  height: 32px;
  border-radius: @radius-base;
  background: @brand-gradient;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  font-weight: @font-weight-bold;
  font-size: @font-size-lg;
  flex-shrink: 0;
}
.logo-name {
  font-size: @font-size-lg;
  font-weight: @font-weight-semibold;
  color: white;
  line-height: 1.2;
}
.logo-sub {
  font-size: 10px;
  color: rgba(255, 255, 255, 0.4);
  letter-spacing: 1px;
}

/* ★ 跨端约定 1：菜单分两组，主菜单组 flex:1 占满，账户组 margin-top:auto 推到底部 */
.sider-menu {
  flex: 1;
  border-right: none !important;
  display: flex;
  flex-direction: column;
  :deep(.menu-group-account) {
    margin-top: auto;
    border-top: 1px solid rgba(255, 255, 255, 0.08);
  }
  :deep(.ant-menu-item-group-title) {
    display: none; /* 分组不显示标题，仅作为容器 */
  }
}

.main-layout { height: 100%; overflow: hidden; }

.header {
  background: @card-bg;
  padding: 0 @spacing-xl;
  display: flex;
  align-items: center;
  height: @header-height;
  box-shadow: @shadow-sm;
  z-index: 10;
}
.header-left {
  display: flex;
  align-items: center;
  gap: @spacing-lg;
  flex: 1;
}
.trigger {
  font-size: 18px;
  color: @text-secondary;
  cursor: pointer;
  &:hover { color: @brand-primary; }
}
.header-search {
  width: 320px;
}
.header-right {
  display: flex;
  align-items: center;
  gap: @spacing-xl;
}
.header-icon {
  font-size: 18px;
  color: @text-secondary;
  cursor: pointer;
  &:hover { color: @brand-primary; }
}
.user-info {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  cursor: pointer;
}
.user-avatar {
  background: @brand-gradient;
  color: white;
}
.user-name {
  font-size: @font-size-sm;
  color: @text-primary;
}

.content {
  flex: 1;
  overflow-y: auto;
  background: @content-bg;
}
</style>
```

- [ ] **Step 2: 创建 router/index.ts（含 /profile /api 路由）**

```ts
import { createRouter, createWebHistory } from 'vue-router'
import AdminLayout from '@/layouts/AdminLayout.vue'

// admin-web 路由：dashboard 完整实现，permissions/applications/data/analytics 骨架占位，profile/api 复用骨架
const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      component: AdminLayout,
      redirect: '/dashboard',
      children: [
        { path: 'dashboard', name: 'AdminDashboard', component: () => import('@/views/dashboard/index.vue'), meta: { title: '管理工作台' } },
        { path: 'permissions', name: 'Permissions', component: () => import('@/views/permissions/index.vue'), meta: { title: '权限管理' } },
        { path: 'applications', name: 'Applications', component: () => import('@/views/applications/index.vue'), meta: { title: '应用管理' } },
        { path: 'data', name: 'DataGovernance', component: () => import('@/views/data/index.vue'), meta: { title: '数据治理' } },
        { path: 'analytics', name: 'Analytics', component: () => import('@/views/analytics/index.vue'), meta: { title: '分析洞察' } },
        { path: 'profile', name: 'AdminProfile', component: () => import('@/views/profile/index.vue'), meta: { title: '账户设置' } },
        { path: 'api', name: 'AdminApiKeys', component: () => import('@/views/api/index.vue'), meta: { title: 'API Keys 管理' } },
      ],
    },
  ],
})

// 路由标题
router.afterEach((to) => {
  const title = to.meta.title as string | undefined
  document.title = title ? `${title} · 智浚 AI 管理后台` : '智浚 AI · 管理后台'
})

export default router
```

- [ ] **Step 3: 提交**

```bash
git add admin-web/src/layouts/ admin-web/src/router/
git commit -m "feat(admin-web): add layout with grouped menu (account pinned bottom) and ThemeToggle"
```

---

## Task 9: admin-web Dashboard 管理工作台页面（完整实现）

**Files:**
- Create: `d:\AI\DredgeAI\admin-web\src\views\dashboard\index.vue`

> 完整示范页面架构、组件用法、图表集成、mock 调用链路，供 adminWeb 团队学习参考。

- [ ] **Step 1: 创建 Dashboard 页面**

```vue
<template>
  <div class="page-container">
    <PageHeader title="管理工作台" description="平台运行总览 · 实时指标与待办处置" />

    <!-- 顶部指标卡 4 列 -->
    <a-row :gutter="[20, 20]" class="metric-row">
      <a-col v-for="m in appStore.metrics" :key="m.id" :span="6">
        <MetricCard
          :title="m.title"
          :value="m.value"
          :trend="m.trend"
          :trend-up="m.trendUp"
          :sparkline="m.sparkline"
        />
      </a-col>
    </a-row>

    <!-- 主区双栏 -->
    <a-row :gutter="[20, 20]" class="main-row">
      <a-col :span="16">
        <!-- 调用趋势折线图 -->
        <SectionCard title="调用趋势" class="mb-20">
          <template #extra>
            <a-radio-group v-model:value="trendRange" size="small">
              <a-radio-button value="7d">近 7 天</a-radio-button>
              <a-radio-button value="30d">近 30 天</a-radio-button>
            </a-radio-group>
          </template>
          <ChartContainer :option="trendChartOption" height="320px" :loading="chartLoading" />
        </SectionCard>

        <!-- 待办告警列表 -->
        <SectionCard title="待办告警" nopad>
          <template #extra>
            <a-badge :count="pendingAlerts.length" :number-style="{ backgroundColor: '#EF4444' }" />
            <a-button type="link" size="small" class="ml-8">全部告警</a-button>
          </template>
          <a-list :data-source="appStore.alerts" :loading="loading" item-layout="horizontal">
            <template #renderItem="{ item }">
              <a-list-item class="alert-item">
                <a-list-item-meta>
                  <template #avatar>
                    <div class="alert-avatar" :class="`alert-avatar--${item.level}`">
                      <component :is="alertIconMap[item.level]" />
                    </div>
                  </template>
                  <template #title>
                    <span class="alert-title">{{ item.title }}</span>
                  </template>
                  <template #description>
                    <span class="alert-source">{{ item.source }} · {{ item.time }}</span>
                  </template>
                </a-list-item-meta>
                <div class="alert-right">
                  <a-tag :color="levelColorMap[item.level]">{{ item.level }}</a-tag>
                  <a-tag :color="statusColorMap[item.status]">{{ item.status }}</a-tag>
                  <a-button
                    v-if="item.status === '待处理'"
                    type="link"
                    size="small"
                    @click="handleAlert(item)"
                  >
                    立即处理
                  </a-button>
                </div>
              </a-list-item>
            </template>
          </a-list>
        </SectionCard>
      </a-col>

      <a-col :span="8">
        <!-- 应用排行 TOP5 -->
        <SectionCard title="应用排行 TOP5" class="mb-20">
          <ChartContainer :option="rankingChartOption" height="220px" :loading="chartLoading" />
        </SectionCard>

        <!-- 审核提醒 -->
        <SectionCard title="审核提醒" nopad>
          <template #extra>
            <a-button type="link" size="small">全部</a-button>
          </template>
          <a-list :data-source="appStore.reviewReminders" :loading="loading" size="small">
            <template #renderItem="{ item }">
              <a-list-item class="review-item">
                <a-list-item-meta>
                  <template #title>
                    <a-tag :color="reviewTypeColorMap[item.type]" class="review-type">{{ item.type }}</a-tag>
                    <span class="review-applicant">{{ item.applicant }}</span>
                  </template>
                  <template #description>
                    <span class="review-content">{{ item.content }}</span>
                    <div class="review-time">{{ item.time }}</div>
                  </template>
                </a-list-item-meta>
                <div class="review-actions">
                  <a-button type="link" size="small" @click="handleApprove(item)">通过</a-button>
                  <a-button type="link" size="small" danger @click="handleReject(item)">驳回</a-button>
                </div>
              </a-list-item>
            </template>
          </a-list>
        </SectionCard>
      </a-col>
    </a-row>

    <!-- 模型成本分布 -->
    <a-row :gutter="[20, 20]" class="cost-row">
      <a-col :span="24">
        <SectionCard title="模型成本分布（本月）">
          <template #extra>
            <a-button type="link" size="small">成本报表</a-button>
          </template>
          <ChartContainer :option="costPieOption" height="280px" :loading="chartLoading" />
        </SectionCard>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import {
  ExclamationCircleOutlined, WarningOutlined, InfoCircleOutlined,
} from '@ant-design/icons-vue'
import type { Component } from 'vue'
import PageHeader from '@/components/PageHeader.vue'
import SectionCard from '@/components/SectionCard.vue'
import MetricCard from '@/components/MetricCard.vue'
import ChartContainer from '@/components/ChartContainer.vue'
import { useAppStore } from '@/stores/app'
import { getCallTrend, getAppRankingChart, getModelCost } from '@/api/modules/dashboard'
import type { AlertItem, ReviewReminder, LineChartData, BarChartData, PieChartData } from '@/types'

const appStore = useAppStore()
const loading = ref(false)
const chartLoading = ref(false)
const trendRange = ref<'7d' | '30d'>('7d')

const alertIconMap: Record<AlertItem['level'], Component> = {
  '严重': ExclamationCircleOutlined,
  '警告': WarningOutlined,
  '提示': InfoCircleOutlined,
}
const levelColorMap: Record<AlertItem['level'], string> = {
  '严重': 'red',
  '警告': 'orange',
  '提示': 'blue',
}
const statusColorMap: Record<AlertItem['status'], string> = {
  '待处理': 'red',
  '处理中': 'orange',
  '已解决': 'green',
}
const reviewTypeColorMap: Record<ReviewReminder['type'], string> = {
  '应用上架': 'cyan',
  '权限申请': 'blue',
  'Key 申请': 'purple',
  '配额调整': 'orange',
}

const trendData = ref<LineChartData>({ categories: [], series: [] })
const rankingData = ref<BarChartData>({ categories: [], series: [] })
const costData = ref<PieChartData>({ name: '', data: [] })

// 待处理告警
const pendingAlerts = computed(() => appStore.alerts.filter((a) => a.status === '待处理'))

// 调用趋势折线图配置
const trendChartOption = computed(() => ({
  tooltip: { trigger: 'axis' },
  legend: { data: trendData.value.series.map((s) => s.name), bottom: 0 },
  grid: { left: '3%', right: '4%', bottom: '10%', top: '5%', containLabel: true },
  xAxis: { type: 'category', boundaryGap: false, data: trendData.value.categories },
  yAxis: { type: 'value' },
  series: trendData.value.series.map((s, i) => ({
    name: s.name,
    type: 'line',
    smooth: true,
    data: s.data,
    itemStyle: { color: i === 0 ? '#0EA5E9' : '#10B981' },
    areaStyle: { opacity: 0.1 },
  })),
}))

// 应用排行柱状图配置
const rankingChartOption = computed(() => ({
  tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
  grid: { left: '3%', right: '4%', bottom: '3%', top: '5%', containLabel: true },
  xAxis: { type: 'value', axisLabel: { formatter: (v: number) => `${(v / 1000).toFixed(0)}k` } },
  yAxis: { type: 'category', data: rankingData.value.categories, inverse: true },
  series: [{
    type: 'bar',
    data: rankingData.value.series[0]?.data || [],
    itemStyle: { color: '#0EA5E9', borderRadius: [0, 4, 4, 0] },
    barWidth: '60%',
    label: { show: true, position: 'right', formatter: (p: { value: number }) => p.value.toLocaleString() },
  }],
}))

// 模型成本饼图配置
const costPieOption = computed(() => ({
  tooltip: { trigger: 'item', formatter: '{b}: ¥{c} ({d}%)' },
  legend: { bottom: 0, type: 'scroll' },
  series: [{
    type: 'pie',
    radius: ['40%', '70%'],
    avoidLabelOverlap: false,
    itemStyle: { borderRadius: 8, borderColor: '#fff', borderWidth: 2 },
    label: { show: false },
    emphasis: { label: { show: true, fontSize: 14, fontWeight: 'bold' } },
    data: costData.value.data.map((d, i) => ({
      ...d,
      itemStyle: { color: ['#0EA5E9', '#06B6D4', '#10B981', '#F59E0B', '#94A3B8'][i % 5] },
    })),
  }],
}))

// 加载工作台主数据（指标、告警、排行、审核）
async function loadDashboard(): Promise<void> {
  loading.value = true
  try {
    await appStore.fetchDashboard()
  } finally {
    loading.value = false
  }
}

// 加载图表数据
async function loadCharts(): Promise<void> {
  chartLoading.value = true
  try {
    const [trend, ranking, cost] = await Promise.all([
      getCallTrend(), getAppRankingChart(), getModelCost(),
    ])
    trendData.value = trend
    rankingData.value = ranking
    costData.value = cost
  } finally {
    chartLoading.value = false
  }
}

function handleAlert(item: AlertItem): void {
  message.info(`已派单处理：${item.title}`)
}

function handleApprove(item: ReviewReminder): void {
  message.success(`已通过：${item.applicant} 的${item.type}申请`)
}

function handleReject(item: ReviewReminder): void {
  message.warning(`已驳回：${item.applicant} 的${item.type}申请`)
}

onMounted(() => {
  loadDashboard()
  loadCharts()
})
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.metric-row { margin-bottom: @spacing-lg; }
.main-row { margin-bottom: @spacing-lg; }
.mb-20 { margin-bottom: 20px; }
.ml-8 { margin-left: 8px; }

.alert-item {
  padding: @spacing-base @spacing-xl;
  &:hover { background: @divider-color; }
}
.alert-avatar {
  width: 36px;
  height: 36px;
  border-radius: @radius-base;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  font-size: 16px;
  &--严重 { background: @danger; }
  &--警告 { background: @warning; }
  &--提示 { background: @info; }
}
.alert-title {
  font-size: @font-size-base;
  color: @text-primary;
  font-weight: @font-weight-medium;
}
.alert-source {
  font-size: @font-size-xs;
  color: @text-tertiary;
}
.alert-right {
  display: flex;
  align-items: center;
  gap: @spacing-xs;
}

.review-item {
  padding: @spacing-base @spacing-xl;
}
.review-type {
  margin-right: @spacing-sm;
}
.review-applicant {
  font-size: @font-size-sm;
  color: @text-primary;
  font-weight: @font-weight-medium;
}
.review-content {
  font-size: @font-size-xs;
  color: @text-secondary;
}
.review-time {
  font-size: 11px;
  color: @text-tertiary;
  margin-top: 2px;
}
.review-actions {
  display: flex;
  flex-direction: column;
  gap: 0;
}
</style>
```

- [ ] **Step 2: 提交**

```bash
git add admin-web/src/views/dashboard/
git commit -m "feat(admin-web): implement full dashboard page as architecture reference"
```

---

## Task 10: admin-web 6 个骨架占位页（含 profile / api）

**Files:**
- Create: `d:\AI\DredgeAI\admin-web\src\views\permissions\index.vue`
- Create: `d:\AI\DredgeAI\admin-web\src\views\applications\index.vue`
- Create: `d:\AI\DredgeAI\admin-web\src\views\data\index.vue`
- Create: `d:\AI\DredgeAI\admin-web\src\views\analytics\index.vue`
- Create: `d:\AI\DredgeAI\admin-web\src\views\profile\index.vue` ★ 跨端约定 1 置底账户菜单对应页
- Create: `d:\AI\DredgeAI\admin-web\src\views\api\index.vue` ★ 跨端约定 1 置底账户菜单对应页

> 全部使用 Task 7 创建的 `PageSkeleton` 组件，仅传不同的 `title` / `description` / `routeName` / `implementHint`，标注待实现模块与参考清单。

- [ ] **Step 1: 创建 views/permissions/index.vue**

```vue
<template>
  <PageSkeleton
    title="权限管理"
    description="用户、角色、权限组、数据范围管理"
    route-name="permissions"
    implement-hint="用户列表、角色矩阵、权限分配、操作审计"
  />
</template>

<script setup lang="ts">
import PageSkeleton from '@/components/PageSkeleton.vue'

// 权限管理骨架页（待 adminWeb 团队实现）
</script>
```

- [ ] **Step 2: 创建 views/applications/index.vue**

```vue
<template>
  <PageSkeleton
    title="应用管理"
    description="应用上架、下架、版本、配额、灰度发布"
    route-name="applications"
    implement-hint="应用列表、上架审核、版本管理、配额配置"
  />
</template>

<script setup lang="ts">
import PageSkeleton from '@/components/PageSkeleton.vue'

// 应用管理骨架页（待 adminWeb 团队实现）
</script>
```

- [ ] **Step 3: 创建 views/data/index.vue**

```vue
<template>
  <PageSkeleton
    title="数据治理"
    description="数据源、知识库、向量索引、数据脱敏"
    route-name="data"
    implement-hint="数据源接入、知识库管理、索引重建、脱敏规则"
  />
</template>

<script setup lang="ts">
import PageSkeleton from '@/components/PageSkeleton.vue'

// 数据治理骨架页（待 adminWeb 团队实现）
</script>
```

- [ ] **Step 4: 创建 views/analytics/index.vue**

```vue
<template>
  <PageSkeleton
    title="分析洞察"
    description="调用统计、成本分析、模型对比、SLA 监控"
    route-name="analytics"
    implement-hint="调用趋势、成本报表、模型对比、SLA 看板"
  />
</template>

<script setup lang="ts">
import PageSkeleton from '@/components/PageSkeleton.vue'

// 分析洞察骨架页（待 adminWeb 团队实现）
</script>
```

- [ ] **Step 5: 创建 views/profile/index.vue（★ 跨端约定 1：置底「账户设置」对应页）**

```vue
<template>
  <PageSkeleton
    title="账户设置"
    description="管理员个人信息、密码、双因素认证、登录历史"
    route-name="profile"
    implement-hint="个人资料、修改密码、双因素认证、登录设备、操作日志"
  />
</template>

<script setup lang="ts">
import PageSkeleton from '@/components/PageSkeleton.vue'

// 账户设置骨架页（待 adminWeb 团队实现）
</script>
```

- [ ] **Step 6: 创建 views/api/index.vue（★ 跨端约定 1：置底「API Keys 管理」对应页）**

```vue
<template>
  <PageSkeleton
    title="API Keys 管理"
    description="平台级 API Key 生命周期、配额、调用日志、IP 白名单"
    route-name="api"
    implement-hint="Key 列表、新建/吊销、配额策略、调用日志、IP 白名单"
  />
</template>

<script setup lang="ts">
import PageSkeleton from '@/components/PageSkeleton.vue'

// API Keys 管理骨架页（待 adminWeb 团队实现）
</script>
```

- [ ] **Step 7: 提交**

```bash
git add admin-web/src/views/permissions/ admin-web/src/views/applications/ admin-web/src/views/data/ admin-web/src/views/analytics/ admin-web/src/views/profile/ admin-web/src/views/api/
git commit -m "feat(admin-web): add 6 skeleton pages (incl. profile/api pinned bottom)"
```

---

## Task 11: admin-web 验证与启动测试

- [ ] **Step 1: 类型检查**

Run: `pnpm --filter admin-web typecheck`
Expected: 无 TypeScript 错误

- [ ] **Step 2: 构建测试**

Run: `pnpm --filter admin-web build`
Expected: 构建成功，产出 `admin-web/dist/`

- [ ] **Step 3: 启动 dev server 验证**

Run: `pnpm --filter admin-web dev`
Expected:
- 监听 `http://localhost:5374/`
- 浏览器访问 `/dashboard` 显示完整管理工作台（4 指标卡 + 调用趋势折线 + 待办告警列表 + 应用排行 TOP5 + 审核提醒 + 模型成本饼图）
- 访问 `/permissions` `/applications` `/data` `/analytics` `/profile` `/api` 显示骨架占位页
- ★ 跨端约定 1：侧边栏菜单分两组，主菜单在顶部，账户组（账户设置 / API Keys 管理）固定贴底；切换菜单路由正常
- ★ 跨端约定 2：header 右上角有太阳/月亮 ThemeToggle 图标，点击后整体页面、antd 组件、CSS Variables 联动切换 dark/light；刷新后主题保持
- 侧边栏菜单切换正常，刷新后折叠状态保持
- 头部「用户端」图标点击跳转 `http://localhost:5373`

手动验证完成后 Ctrl+C 停止 dev server

- [ ] **Step 4: 提交验证记录**

```bash
git add -A
git commit -m "chore(admin-web): verify typecheck + build + dev server"
```

---

## Task 12: 删除旧 platform/ 目录

**Files:**
- Delete: `d:\AI\DredgeAI\platform\` （整个目录）

> 重构完成，旧的双布局原型不再需要。确认 user-web（5373）与 admin-web（5374）均能独立启动后再删除。

- [ ] **Step 1: 确认双端可独立启动**

Run: `pnpm dev`
Expected:
- user-web 监听 5373
- admin-web 监听 5374
- 两个 dev server 均无报错

手动确认后 Ctrl+C 停止

- [ ] **Step 2: 删除 platform 目录**

Run: `git rm -rf platform/`
Expected: 删除成功

- [ ] **Step 3: 检查根 package.json 与文档中的 platform 引用**

Run: `git grep -n "platform/" -- ':!docs/superpowers/'`
Expected: 无业务代码引用（docs/superpowers 下的历史文档可保留）

如有引用，逐个修正为新架构路径

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "chore: remove legacy platform/ after dual-web refactor complete"
```

---

## 完成清单

- [ ] admin-web 独立工程，端口 5374，与 user-web 完全平行
- [ ] 复用同一套设计 token（sky 主色 + cyan 强调 + slate 深色侧边栏）
- [ ] ★ 跨端约定 1：侧边栏菜单分组，账户组（账户设置 / API Keys 管理）固定贴底
- [ ] ★ 跨端约定 2：右上角 ThemeToggle 切换 dark/light；颜色 token 统一在 themes.less 用 CSS Variables 管理，业务样式文件不再写暗色覆盖
- [ ] ★ 跨端约定 3：user-web AI 应用页面（AI 审标 / 标准查询）落地区分「当前任务」与「历史记录」双区结构，`AppSession` 契约与 `AppSessionHistory.vue` 组件首期落地（详见 user-web 计划修订）
- [ ] mock 由代码常量 `USE_MOCK` 控制，不依赖 .env
- [ ] Dashboard 完整实现：4 指标卡 + 调用趋势折线 + 待办告警 + 应用排行 + 审核提醒 + 模型成本饼图
- [ ] 6 个骨架页（permissions / applications / data / analytics / profile / api）使用统一 PageSkeleton 组件
- [ ] PageSkeleton 标注待实现模块 + 架构参考清单，供 adminWeb 团队学习
- [ ] 旧 `platform/` 目录已删除
- [ ] 类型检查 + 构建 + dev server 验证全部通过
