# 双端独立架构重构实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将现有 `platform/` 单仓双布局原型，重构为 `user-web`（端口 5373，完整实现 6 个页面）与 `admin-web`（端口 5374，dashboard 完整 + 4 个骨架页）两个独立 Vite 工程。

**Architecture:** pnpm workspace 编排两个独立 Vite 应用，各自维护代码、共享同一套设计 token 与视觉语言。axios + axios-mock-adapter 在前端拦截请求返回静态 mock，模拟真实接口调用。UnoCSS 提供原子化样式，Less + variables.less 承载设计 token。

**Tech Stack:** Vue 3.5 + TypeScript 5.7 + Vite 6 + Pinia 2 + Vue Router 4 + ant-design-vue 4 + UnoCSS + Less + axios 1 + axios-mock-adapter + echarts 5 + vue-echarts 7 + dayjs + lodash-es + @vueuse/core + nprogress

**设计 spec:** `docs/superpowers/specs/2026-07-17-dual-web-refactor-design.md`

---

## 文件结构总览

### 根 workspace
```
d:\AI\DredgeAI\
├── package.json              # 根 workspace（脚本编排）
├── pnpm-workspace.yaml
├── tsconfig.base.json
├── .npmrc
```

### user-web (端口 5373)
```
user-web/
├── package.json / vite.config.ts / tsconfig.json / tsconfig.node.json
├── index.html / env.d.ts
└── src/
    ├── api/{request.ts, mock/index.ts, modules/*.ts}
    ├── mock/*.ts
    ├── types/index.ts
    ├── utils/{request.ts, constants.ts, format.ts}
    ├── composables/useTable.ts
    ├── components/{PageHeader,SectionCard,MetricCard,StatusTag,SearchInput,ChartContainer,DataSkeleton,EmptyState}.vue
    ├── layouts/UserLayout.vue
    ├── router/index.ts
    ├── stores/{user.ts, app.ts}
    ├── styles/{variables.less, reset.less, global.less}
    ├── uno.config.ts
    ├── App.vue / main.ts
    └── views/{dashboard,apps,bid-review,standards,profile,api}/index.vue
```

### admin-web (端口 5374)
```
admin-web/
├── package.json / vite.config.ts / tsconfig.json / tsconfig.node.json
├── index.html / env.d.ts
└── src/
    ├── api/{request.ts, mock/index.ts, modules/dashboard.ts}
    ├── mock/{dashboard.ts, chart.ts}
    ├── types/index.ts
    ├── utils/format.ts
    ├── components/{PageHeader,SectionCard,MetricCard,PageSkeleton,ChartContainer,DataSkeleton}.vue
    ├── layouts/AdminLayout.vue
    ├── router/index.ts
    ├── stores/app.ts
    ├── styles/{variables.less, reset.less, global.less}
    ├── uno.config.ts
    ├── App.vue / main.ts
    └── views/{dashboard,permissions,applications,data,analytics}/index.vue
```

---

## Task 1: 创建 pnpm workspace 基础设施

**Files:**
- Create: `d:\AI\DredgeAI\pnpm-workspace.yaml`
- Create: `d:\AI\DredgeAI\.npmrc`
- Create: `d:\AI\DredgeAI\tsconfig.base.json`
- Create: `d:\AI\DredgeAI\package.json`

- [ ] **Step 1: 创建 pnpm-workspace.yaml**

```yaml
packages:
  - 'user-web'
  - 'admin-web'
```

- [ ] **Step 2: 创建 .npmrc**

```ini
auto-install-peers=true
strict-peer-dependencies=false
shamefully-hoist=false
```

- [ ] **Step 3: 创建 tsconfig.base.json**

```json
{
  "compilerOptions": {
    "target": "ES2020",
    "useDefineForClassFields": true,
    "module": "ESNext",
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "skipLibCheck": true,
    "moduleResolution": "bundler",
    "allowImportingTsExtensions": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "preserve",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "forceConsistentCasingInFileNames": true,
    "esModuleInterop": true
  }
}
```

- [ ] **Step 4: 创建根 package.json**

```json
{
  "name": "dredge-ai-workspace",
  "private": true,
  "version": "0.1.0",
  "type": "module",
  "scripts": {
    "dev:user": "pnpm --filter user-web dev",
    "dev:admin": "pnpm --filter admin-web dev",
    "dev": "concurrently -n user,admin -c cyan,magenta \"pnpm dev:user\" \"pnpm dev:admin\"",
    "build:user": "pnpm --filter user-web build",
    "build:admin": "pnpm --filter admin-web build",
    "build": "pnpm build:user && pnpm build:admin",
    "preview:user": "pnpm --filter user-web preview",
    "preview:admin": "pnpm --filter admin-web preview",
    "typecheck": "pnpm --filter user-web typecheck && pnpm --filter admin-web typecheck"
  },
  "devDependencies": {
    "concurrently": "^9.1.0"
  },
  "packageManager": "pnpm@9.1.4"
}
```

- [ ] **Step 5: 安装根依赖**

Run: `pnpm install`
Expected: 创建 `pnpm-lock.yaml`，无报错

- [ ] **Step 6: 提交**

```bash
git add pnpm-workspace.yaml .npmrc tsconfig.base.json package.json pnpm-lock.yaml
git commit -m "chore: setup pnpm workspace root"
```

---

## Task 2: user-web 工程脚手架与依赖

**Files:**
- Create: `d:\AI\DredgeAI\user-web\package.json`
- Create: `d:\AI\DredgeAI\user-web\vite.config.ts`
- Create: `d:\AI\DredgeAI\user-web\tsconfig.json`
- Create: `d:\AI\DredgeAI\user-web\tsconfig.node.json`
- Create: `d:\AI\DredgeAI\user-web\index.html`
- Create: `d:\AI\DredgeAI\user-web\env.d.ts`
- Create: `d:\AI\DredgeAI\user-web\src\App.vue`

- [ ] **Step 1: 创建 user-web/package.json**

```json
{
  "name": "user-web",
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

- [ ] **Step 2: 创建 user-web/vite.config.ts**

```ts
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import UnoCSS from 'unocss/vite'
import { resolve } from 'path'

// user-web vite 配置：端口 5373
export default defineConfig({
  plugins: [vue(), UnoCSS()],
  resolve: {
    alias: { '@': resolve(__dirname, 'src') },
  },
  server: { port: 5373, host: true, open: false },
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

- [ ] **Step 3: 创建 user-web/tsconfig.json**

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

- [ ] **Step 4: 创建 user-web/tsconfig.node.json**

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

- [ ] **Step 5: 创建 user-web/env.d.ts**

```ts
/// <reference types="vite/client" />

declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<Record<string, never>, Record<string, never>, unknown>
  export default component
}
```

- [ ] **Step 6: 创建 user-web/index.html**

```html
<!doctype html>
<html lang="zh-CN">
  <head>
    <meta charset="UTF-8" />
    <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>智浚 AI · 用户端</title>
  </head>
  <body>
    <div id="app"></div>
    <script type="module" src="/src/main.ts"></script>
  </body>
</html>
```

- [ ] **Step 7: 创建 user-web/src/App.vue**

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
git add user-web/
git commit -m "feat(user-web): scaffold project with vite + ts + unocss"
```

---

## Task 3: user-web 设计 token 与全局样式

**Files:**
- Create: `d:\AI\DredgeAI\user-web\src\styles\variables.less`
- Create: `d:\AI\DredgeAI\user-web\src\styles\reset.less`
- Create: `d:\AI\DredgeAI\user-web\src\styles\global.less`
- Create: `d:\AI\DredgeAI\user-web\src\uno.config.ts`

- [ ] **Step 1: 创建 variables.less（设计 token）**

```less
// 品牌色
@brand-primary: #0EA5E9;
@brand-primary-hover: #0284C7;
@brand-gradient: linear-gradient(135deg, #0EA5E9 0%, #06B6D4 100%);
@accent: #06B6D4;

// 状态色
@success: #10B981;
@warning: #F59E0B;
@danger: #EF4444;
@info: #3B82F6;

// 中性色
@sidebar-bg: #0F172A;
@sidebar-bg-2: #1E293B;
@content-bg: #F8FAFC;
@card-bg: #FFFFFF;
@text-primary: #0F172A;
@text-secondary: #475569;
@text-tertiary: #94A3B8;
@border-color: #E2E8F0;
@divider-color: #F1F5F9;

// 字号
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

// 阴影
@shadow-sm: 0 1px 2px rgb(0 0 0 / 0.05);
@shadow-md: 0 4px 12px rgb(15 23 42 / 0.08);
@shadow-lg: 0 12px 32px rgb(15 23 42 / 0.12);
@shadow-brand: 0 8px 24px rgb(14 165 233 / 0.25);

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

- [ ] **Step 2: 创建 reset.less**

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

- [ ] **Step 3: 创建 global.less**

```less
@import './variables.less';

body {
  font-family: @font-family;
  background-color: @content-bg;
  color: @text-primary;
  font-size: @font-size-base;
  line-height: 1.6;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
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

.ant-card {
  border-radius: @radius-lg !important;
  border-color: @border-color !important;
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

- [ ] **Step 4: 创建 uno.config.ts**

```ts
import { defineConfig, presetUno, presetAttributify, presetIcons } from 'unocss'

// UnoCSS 配置：映射设计 token 为原子类
export default defineConfig({
  presets: [
    presetUno(),
    presetAttributify(),
    presetIcons({ scale: 1.2, warn: true }),
  ],
  theme: {
    colors: {
      brand: { DEFAULT: '#0EA5E9', hover: '#0284C7' },
      accent: '#06B6D4',
      success: '#10B981',
      warning: '#F59E0B',
      danger: '#EF4444',
      info: '#3B82F6',
      sidebar: { DEFAULT: '#0F172A', 2: '#1E293B' },
      content: '#F8FAFC',
      card: '#FFFFFF',
      text: { primary: '#0F172A', secondary: '#475569', tertiary: '#94A3B8' },
      border: '#E2E8F0',
      divider: '#F1F5F9',
    },
    boxShadow: {
      sm: '0 1px 2px rgb(0 0 0 / 0.05)',
      md: '0 4px 12px rgb(15 23 42 / 0.08)',
      lg: '0 12px 32px rgb(15 23 42 / 0.12)',
      brand: '0 8px 24px rgb(14 165 233 / 0.25)',
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

- [ ] **Step 5: 提交**

```bash
git add user-web/src/styles/ user-web/src/uno.config.ts
git commit -m "feat(user-web): add design tokens and global styles"
```

---

## Task 4: user-web 类型定义与工具函数

**Files:**
- Create: `d:\AI\DredgeAI\user-web\src\types\index.ts`
- Create: `d:\AI\DredgeAI\user-web\src\utils\constants.ts`
- Create: `d:\AI\DredgeAI\user-web\src\utils\format.ts`
- Create: `d:\AI\DredgeAI\user-web\src\utils\request.ts`

- [ ] **Step 1: 创建 types/index.ts**

```ts
// 用户信息
export interface UserInfo {
  id: string
  name: string
  department: string
  position: string
  email: string
  phone: string
  avatar?: string
  authorizedScopes: string[]
  preferences: { theme: 'light' | 'dark'; language: 'zh-CN' | 'en-US' }
}

// 指标卡
export interface MetricCard {
  id: string
  title: string
  value: string | number
  trend?: string
  trendUp?: boolean
  sparkline?: number[]
}

// 应用卡片
export interface AppCard {
  id: string
  title: string
  description: string
  category: '日常办公' | '专业业务' | '知识查询' | '开发接口'
  icon: string
  status: '已授权' | '待申请' | '已下架'
  route?: string
  version?: string
  pinned?: boolean
}

// 任务
export interface TaskItem {
  id: string
  title: string
  status: '进行中' | '已完成' | '已暂停' | '已失败'
  updatedAt: string
  app?: string
  progress?: number
}

// 文件
export interface FileItem {
  id: string
  name: string
  type: 'pdf' | 'docx' | 'xlsx' | 'pptx' | 'image' | 'other'
  size: string
  updatedAt: string
  url?: string
}

// 审标步骤
export interface BidReviewStep {
  title: string
  description: string
  status: 'wait' | 'process' | 'finish' | 'error'
}

// 风险项
export interface RiskItem {
  id: string
  level: '高风险' | '中风险' | '低风险'
  content: string
  source: string
  suggestion?: string
}

// 审标会话历史
export interface BidReviewSession {
  id: string
  document: string
  date: string
  riskCount: number
  status: '已完成' | '进行中'
  snippets?: { role: 'user' | 'assistant'; content: string }[]
}

// 标准查询结果
export interface StandardResult {
  id: string
  code: string
  title: string
  match: string
  excerpt: string
  source?: string
}

// 标准查询历史
export interface StandardSearchHistory {
  id: string
  query: string
  date: string
  resultCount: number
}

// 标准分类
export interface StandardCategory {
  id: string
  name: string
  count: number
  children?: StandardCategory[]
}

// API Key
export interface ApiKey {
  id: string
  name: string
  key: string
  fullKey: string
  modelType: string
  createdAt: string
  status: '启用' | '禁用'
  usage: number
  quota: number
  docUrl: string
}

// 模型类型
export interface ModelType {
  id: string
  name: string
  provider: string
  description?: string
}

// 用量统计
export interface UsageByModel { modelName: string; calls: number; share: number }
export interface UsageByKey { keyName: string; calls: number; share: number }

// 通知
export interface Notification {
  id: string
  type: 'system' | 'business' | 'audit'
  title: string
  content: string
  time: string
  read: boolean
}

// 图表数据
export interface ChartSeries { name: string; data: number[] }
export interface LineChartData { categories: string[]; series: ChartSeries[] }
export interface PieChartData { name: string; data: { name: string; value: number }[] }
```

- [ ] **Step 2: 创建 utils/constants.ts**

```ts
// 认证令牌 localStorage key
export const STORAGE_TOKEN_KEY = 'DREDGE_AI_TOKEN'

// 跨端跳转地址
export const ADMIN_WEB_URL = 'http://localhost:5374'
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

// 文件大小格式化
export function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}
```

- [ ] **Step 5: 提交**

```bash
git add user-web/src/types/ user-web/src/utils/
git commit -m "feat(user-web): add type definitions and utils"
```

---

## Task 5: user-web Mock 数据

**Files:**
- Create: `d:\AI\DredgeAI\user-web\src\mock\user.ts`
- Create: `d:\AI\DredgeAI\user-web\src\mock\app.ts`
- Create: `d:\AI\DredgeAI\user-web\src\mock\task.ts`
- Create: `d:\AI\DredgeAI\user-web\src\mock\file.ts`
- Create: `d:\AI\DredgeAI\user-web\src\mock\bid.ts`
- Create: `d:\AI\DredgeAI\user-web\src\mock\standard.ts`
- Create: `d:\AI\DredgeAI\user-web\src\mock\apikey.ts`
- Create: `d:\AI\DredgeAI\user-web\src\mock\notification.ts`
- Create: `d:\AI\DredgeAI\user-web\src\mock\chart.ts`

- [ ] **Step 1: 创建 mock/user.ts**

```ts
import type { UserInfo } from '@/types'

// 当前登录用户信息
export const currentUser: UserInfo = {
  id: 'u-001',
  name: '张明',
  department: '工程技术部',
  position: '高级工程师',
  email: 'zhangming@dredgeai.com',
  phone: '138****6688',
  authorizedScopes: ['AI 审标', '标准查询', '智能写作', '合同审查', '数据看板', '知识问答'],
  preferences: { theme: 'light', language: 'zh-CN' },
}
```

- [ ] **Step 2: 创建 mock/app.ts**

```ts
import type { AppCard } from '@/types'

// 应用广场分类
export const categories = [
  { key: 'all', label: '全部' },
  { key: '日常办公', label: '日常办公' },
  { key: '专业业务', label: '专业业务' },
  { key: '知识查询', label: '知识查询' },
  { key: '开发接口', label: '开发接口' },
] as const

// 应用卡片列表
export const appCards: AppCard[] = [
  { id: '1', title: 'AI 审标', description: '智能识别招标文件中的风险条款与偏差', category: '专业业务', icon: 'FileSearchOutlined', status: '已授权', route: '/bid-review', version: 'v2.3.1', pinned: true },
  { id: '2', title: '标准查询', description: '自然语言检索行业标准与规范条款', category: '知识查询', icon: 'BookOutlined', status: '已授权', route: '/standards', version: 'v1.5.0', pinned: true },
  { id: '3', title: '智能写作', description: 'AI 辅助撰写工程报告与文档', category: '日常办公', icon: 'EditOutlined', status: '已授权', version: 'v3.0.2' },
  { id: '4', title: '合同审查', description: '自动识别合同风险条款与合规问题', category: '专业业务', icon: 'SafetyOutlined', status: '已授权', version: 'v1.2.0' },
  { id: '5', title: '数据看板', description: '可视化展示项目关键指标与趋势', category: '日常办公', icon: 'DashboardOutlined', status: '已授权', version: 'v1.8.0' },
  { id: '6', title: 'API 网关', description: '统一管理第三方 AI 服务接入与调用', category: '开发接口', icon: 'ApiOutlined', status: '待申请', route: '/api' },
  { id: '7', title: '知识问答', description: '基于企业知识库的智能问答系统', category: '知识查询', icon: 'QuestionCircleOutlined', status: '已授权', version: 'v2.0.0' },
  { id: '8', title: '文档比对', description: '智能比对多个文档版本差异', category: '专业业务', icon: 'SwapOutlined', status: '待申请' },
  { id: '9', title: '代码助手', description: '面向研发团队的智能代码补全与审查', category: '开发接口', icon: 'CodeOutlined', status: '待申请' },
  { id: '10', title: '会议纪要', description: '自动生成会议摘要与待办事项', category: '日常办公', icon: 'TeamOutlined', status: '已授权', version: 'v1.1.0' },
]
```

- [ ] **Step 3: 创建 mock/task.ts**

```ts
import type { TaskItem } from '@/types'

// 最近任务列表
export const taskItems: TaskItem[] = [
  { id: '1', title: 'XX 项目招标文件风险分析', status: '进行中', updatedAt: '2026-07-17 14:32', app: 'AI 审标', progress: 65 },
  { id: '2', title: 'GB/T 19001 条款匹配检查', status: '已完成', updatedAt: '2026-07-16 16:08', app: '标准查询', progress: 100 },
  { id: '3', title: '合同审查 - 供应商协议 v3', status: '已完成', updatedAt: '2026-07-15 11:24', app: '合同审查', progress: 100 },
  { id: '4', title: '技术规范文档比对', status: '进行中', updatedAt: '2026-07-14 09:50', app: '文档比对', progress: 40 },
  { id: '5', title: '本周工程报告撰写', status: '已暂停', updatedAt: '2026-07-13 17:15', app: '智能写作', progress: 30 },
  { id: '6', title: 'Q2 项目数据看板生成', status: '已完成', updatedAt: '2026-07-12 10:30', app: '数据看板', progress: 100 },
]

// 推荐任务（首页快捷入口）
export const quickTasks = [
  { id: 'qt-1', title: '开始 AI 审标', tag: '专业业务', route: '/bid-review', icon: 'FileSearchOutlined' },
  { id: 'qt-2', title: '查询标准条款', tag: '知识查询', route: '/standards', icon: 'BookOutlined' },
  { id: 'qt-3', title: '撰写工程报告', tag: '日常办公', route: '/apps', icon: 'EditOutlined' },
] as const
```

- [ ] **Step 4: 创建 mock/file.ts**

```ts
import type { FileItem } from '@/types'

// 最近文件列表
export const fileItems: FileItem[] = [
  { id: '1', name: 'XX_项目_招标文件.pdf', type: 'pdf', size: '2.4 MB', updatedAt: '2026-07-17 14:30' },
  { id: '2', name: '合同审查报告_v3.docx', type: 'docx', size: '1.1 MB', updatedAt: '2026-07-16 16:05' },
  { id: '3', name: '标准查询结果_20260715.xlsx', type: 'xlsx', size: '386 KB', updatedAt: '2026-07-15 11:20' },
  { id: '4', name: '项目需求文档_v2.pdf', type: 'pdf', size: '1.8 MB', updatedAt: '2026-07-14 09:45' },
  { id: '5', name: '会议纪要_0713.docx', type: 'docx', size: '124 KB', updatedAt: '2026-07-13 17:10' },
  { id: '6', name: 'Q2_数据看板截图.png', type: 'image', size: '892 KB', updatedAt: '2026-07-12 10:25' },
]
```

- [ ] **Step 5: 创建 mock/bid.ts**

```ts
import type { BidReviewStep, RiskItem, BidReviewSession } from '@/types'

// 审标步骤
export const bidReviewSteps: BidReviewStep[] = [
  { title: '上传文档', description: '支持 PDF/Word 格式，单文件 ≤ 50MB', status: 'finish' },
  { title: '智能识别', description: 'AI 识别关键条款与结构化信息', status: 'finish' },
  { title: '风险分析', description: '风险分级与处置建议', status: 'process' },
  { title: '输出报告', description: '导出分析报告与原文标注', status: 'wait' },
]

// 风险项
export const riskItems: RiskItem[] = [
  { id: 'r-1', level: '高风险', content: '投标截止时间与法定节假日冲突，可能导致无效投标', source: '第3章 2.1节', suggestion: '建议核实节假日安排并申请延期' },
  { id: 'r-2', level: '中风险', content: '资质要求中"近三年"定义不明确，存在歧义', source: '第5章 1.3节', suggestion: '建议在投标澄清阶段提出书面询问' },
  { id: 'r-3', level: '中风险', content: '技术评分项权重过高，可能影响商务竞争力', source: '第6章 4.1节', suggestion: '建议加强技术方案论证' },
  { id: 'r-4', level: '低风险', content: '付款条款中质保金比例高于行业惯例', source: '第8章 4.2节', suggestion: '可在商务谈判中协商调整' },
  { id: 'r-5', level: '低风险', content: '履约保证金缴纳期限偏短', source: '第9章 2.3节', suggestion: '建议提前做好资金安排' },
]

// 审标会话历史
export const bidReviewSessions: BidReviewSession[] = [
  { id: 's-1', document: 'XX_项目_招标文件.pdf', date: '2026-07-17 14:32', riskCount: 5, status: '进行中', snippets: [
    { role: 'user', content: '重点检查第 3 章和第 8 章' },
    { role: 'assistant', content: '已识别 5 项风险，其中高风险 1 项、中风险 2 项、低风险 2 项。详见右侧风险面板。' },
  ]},
  { id: 's-2', document: 'YY_工程_投标文件.pdf', date: '2026-07-15 09:18', riskCount: 5, status: '已完成' },
  { id: 's-3', document: 'ZZ_项目_招标文件_v2.pdf', date: '2026-07-12 16:40', riskCount: 2, status: '已完成' },
  { id: 's-4', document: 'WW_改造_招标文件.pdf', date: '2026-07-08 11:05', riskCount: 0, status: '已完成' },
  { id: 's-5', document: 'AA_咨询_招标文件.pdf', date: '2026-07-03 14:22', riskCount: 3, status: '已完成' },
]

// 文档原文片段
export const bidDocumentExcerpt = `第三章 投标文件编制

2.1 投标截止时间
投标文件应于 2026 年 8 月 15 日 17:00 前送达指定地点，逾期不予受理。

第五章 资格审查

1.3 资质要求
投标人须具备近三年内类似项目业绩不少于 3 项，且具有相应资质等级。

第六章 评标办法

4.1 评分权重
技术评分 60%，商务评分 30%，价格评分 10%。

第八章 合同条款

4.2 质保金
质保金为合同总价的 5%，质保期满后无息返还。`
```

- [ ] **Step 6: 创建 mock/standard.ts**

```ts
import type { StandardResult, StandardSearchHistory, StandardCategory } from '@/types'

// 查询历史
export const standardsSearchHistory: StandardSearchHistory[] = [
  { id: 'h-1', query: 'GB/T 19001 质量管理体系', date: '2026-07-17 10:15', resultCount: 3 },
  { id: 'h-2', query: '施工质量验收标准', date: '2026-07-16 14:38', resultCount: 5 },
  { id: 'h-3', query: '合同审查相关规范', date: '2026-07-14 09:22', resultCount: 2 },
  { id: 'h-4', query: '安全生产标准化', date: '2026-07-10 16:50', resultCount: 4 },
]

// 命中结果
export const standardsResult: StandardResult[] = [
  { id: 'std-1', code: 'GB/T 19001-2016', title: '质量管理体系 要求', match: '条款 7.1.4 — 过程运行环境', excerpt: '组织应确定、提供并维护所需的过程运行环境，以获得合格产品和服务。', source: '国家标准全文公开系统' },
  { id: 'std-2', code: 'GB/T 50430-2017', title: '工程建设施工企业质量管理规范', match: '条款 3.2 — 质量管理体系策划', excerpt: '施工企业应建立并实施质量管理体系，并持续改进其有效性。', source: '国家标准全文公开系统' },
  { id: 'std-3', code: 'GB 50300-2013', title: '建筑工程施工质量验收统一标准', match: '条款 4.0 — 验收基本规定', excerpt: '建筑工程施工质量应按下列要求进行验收：参与验收各方人员应具备规定的资格。', source: '国家标准全文公开系统' },
  { id: 'std-4', code: 'GB/T 28001-2011', title: '职业健康安全管理体系 要求', match: '条款 4.4.6 — 运行控制', excerpt: '组织应确定与所认定的风险相关的、需要采取控制措施的运行和活动。', source: '国家标准全文公开系统' },
  { id: 'std-5', code: 'JGJ 59-2011', title: '建筑施工安全检查标准', match: '条款 3 — 检查评分', excerpt: '建筑施工安全检查评定中保证项目应全数检查，保证项目得分必须为合格。', source: '行业标准全文公开系统' },
]

// 标准分类树
export const standardCategories: StandardCategory[] = [
  { id: 'c-1', name: '国家标准（GB）', count: 1250, children: [
    { id: 'c-1-1', name: '工程建设', count: 320 },
    { id: 'c-1-2', name: '质量管理', count: 180 },
    { id: 'c-1-3', name: '安全环保', count: 210 },
  ]},
  { id: 'c-2', name: '行业标准（JGJ）', count: 680 },
  { id: 'c-3', name: '地方标准（DB）', count: 420 },
  { id: 'c-4', name: '团体标准（T）', count: 280 },
]

// 推荐问题
export const recommendedQuestions = [
  '质量管理体系运行环境有哪些要求？',
  '施工质量验收的基本规定是什么？',
  '职业健康安全管理体系如何运行控制？',
  '建筑施工安全检查如何评分？',
]
```

- [ ] **Step 7: 创建 mock/apikey.ts**

```ts
import type { ApiKey, ModelType, UsageByModel, UsageByKey } from '@/types'

// API Key 列表
export const apiKeys: ApiKey[] = [
  { id: 'k-1', name: '生产环境', key: 'sk-dg-****-a1b2', fullKey: 'sk-dg-prod-a1b2c3d4e5f6', modelType: 'GPT-4o', createdAt: '2026-06-01', status: '启用', usage: 12500, quota: 50000, docUrl: 'https://docs.dredgeai.com/api/gpt4o' },
  { id: 'k-2', name: '测试环境', key: 'sk-dg-****-f6e5', fullKey: 'sk-dg-test-f6e5d4c3b2a1', modelType: 'Claude 3.5 Sonnet', createdAt: '2026-06-15', status: '启用', usage: 8300, quota: 20000, docUrl: 'https://docs.dredgeai.com/api/claude' },
  { id: 'k-3', name: '第三方集成', key: 'sk-dg-****-x7y8', fullKey: 'sk-dg-integ-x7y8z9a0b1c2', modelType: 'DeepSeek-V3', createdAt: '2026-07-01', status: '禁用', usage: 0, quota: 10000, docUrl: 'https://docs.dredgeai.com/api/deepseek' },
  { id: 'k-4', name: 'AI 审标专用', key: 'sk-dg-****-m3n4', fullKey: 'sk-dg-review-m3n4o5p6q7r8', modelType: 'GPT-4o', createdAt: '2026-06-20', status: '启用', usage: 5600, quota: 30000, docUrl: 'https://docs.dredgeai.com/api/gpt4o' },
]

// 模型类型
export const modelTypes: ModelType[] = [
  { id: 'gpt4o', name: 'GPT-4o', provider: 'OpenAI', description: '通用旗舰模型，适合复杂推理' },
  { id: 'claude35', name: 'Claude 3.5 Sonnet', provider: 'Anthropic', description: '长文本与代码能力突出' },
  { id: 'deepseek', name: 'DeepSeek-V3', provider: 'DeepSeek', description: '国产高性价比模型' },
  { id: 'qwen', name: '通义千问-Max', provider: '阿里云', description: '中文场景优化' },
  { id: 'local', name: '本地模型', provider: '自部署', description: '数据不出域的私有部署' },
]

export const usageByModel: UsageByModel[] = [
  { modelName: 'GPT-4o', calls: 18100, share: 45 },
  { modelName: 'Claude 3.5 Sonnet', calls: 8300, share: 30 },
  { modelName: '本地模型', calls: 4200, share: 15 },
  { modelName: '通义千问-Max', calls: 1600, share: 6 },
  { modelName: 'DeepSeek-V3', calls: 800, share: 4 },
]

export const usageByKey: UsageByKey[] = [
  { keyName: '生产环境', calls: 12500, share: 38 },
  { keyName: '测试环境', calls: 8300, share: 25 },
  { keyName: 'AI 审标专用', calls: 5600, share: 17 },
]
```

- [ ] **Step 8: 创建 mock/notification.ts**

```ts
import type { Notification } from '@/types'

// 通知列表
export const notifications: Notification[] = [
  { id: 'n-1', type: 'business', title: 'AI 审标任务完成', content: 'XX 项目招标文件已完成风险分析，共识别 5 项风险', time: '2026-07-17 14:35', read: false },
  { id: 'n-2', type: 'system', title: '系统维护通知', content: '今晚 23:00-次日 02:00 系统例行维护，期间服务可能中断', time: '2026-07-17 10:00', read: false },
  { id: 'n-3', type: 'audit', title: 'API Key 创建', content: '您于 2026-07-01 创建了 "第三方集成" Key', time: '2026-07-01 09:30', read: true },
  { id: 'n-4', type: 'business', title: '标准查询结果', content: '您查询的 "施工质量验收标准" 已命中 5 条标准', time: '2026-07-16 14:40', read: true },
  { id: 'n-5', type: 'system', title: '权限更新', content: '管理员已为您开通 "数据看板" 应用权限', time: '2026-07-15 16:20', read: true },
  { id: 'n-6', type: 'audit', title: '登录提醒', content: '您的账号于 2026-07-15 08:50 在新设备登录', time: '2026-07-15 08:50', read: true },
  { id: 'n-7', type: 'business', title: '合同审查报告生成', content: '供应商协议 v3 审查报告已生成，共 3 处风险', time: '2026-07-15 11:25', read: true },
  { id: 'n-8', type: 'system', title: '版本更新', content: 'AI 审标应用已升级到 v2.3.1', time: '2026-07-14 18:00', read: true },
]
```

- [ ] **Step 9: 创建 mock/chart.ts**

```ts
import type { LineChartData, PieChartData } from '@/types'

// 个人效率趋势（近 7 天）
export const efficiencyTrend: LineChartData = {
  categories: ['7/11', '7/12', '7/13', '7/14', '7/15', '7/16', '7/17'],
  series: [
    { name: '任务数', data: [3, 5, 2, 4, 6, 4, 7] },
    { name: '完成数', data: [2, 4, 2, 3, 5, 4, 5] },
  ],
}

// API 用量按模型分布
export const apiKeyUsagePie: PieChartData = {
  name: 'API 用量分布',
  data: [
    { name: 'GPT-4o', value: 18100 },
    { name: 'Claude 3.5 Sonnet', value: 8300 },
    { name: '本地模型', value: 4200 },
    { name: '通义千问-Max', value: 1600 },
    { name: 'DeepSeek-V3', value: 800 },
  ],
}

// API 用量按 Key 分布
export const apiKeyUsageBar = {
  categories: ['生产环境', '测试环境', 'AI 审标专用'],
  series: [{ name: '调用次数', data: [12500, 8300, 5600] }],
}
```

- [ ] **Step 10: 提交**

```bash
git add user-web/src/mock/
git commit -m "feat(user-web): add mock data for all modules"
```

---

## Task 6: user-web API 层（axios + mock-adapter）

**Files:**
- Create: `d:\AI\DredgeAI\user-web\src\api\request.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\mock\index.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\mock\modules\user.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\mock\modules\app.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\mock\modules\task.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\mock\modules\file.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\mock\modules\bid.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\mock\modules\standard.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\mock\modules\apikey.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\mock\modules\notification.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\modules\user.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\modules\app.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\modules\task.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\modules\file.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\modules\bid.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\modules\standard.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\modules\apikey.ts`
- Create: `d:\AI\DredgeAI\user-web\src\api\modules\notification.ts`

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
import { registerUserMock } from './modules/user'
import { registerAppMock } from './modules/app'
import { registerTaskMock } from './modules/task'
import { registerFileMock } from './modules/file'
import { registerBidMock } from './modules/bid'
import { registerStandardMock } from './modules/standard'
import { registerApiKeyMock } from './modules/apikey'
import { registerNotificationMock } from './modules/notification'

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

  registerUserMock(mock, wrap)
  registerAppMock(mock, wrap)
  registerTaskMock(mock, wrap)
  registerFileMock(mock, wrap)
  registerBidMock(mock, wrap)
  registerStandardMock(mock, wrap)
  registerApiKeyMock(mock, wrap)
  registerNotificationMock(mock, wrap)
}
```

- [ ] **Step 3: 创建 mock 模块注册函数（8 个文件，模式相同）**

每个 `api/mock/modules/*.ts` 文件导出一个 `register*Mock` 函数，调用 `mock.onGet('/path').reply(wrap(() => data))`。

`api/mock/modules/user.ts`：
```ts
import type MockAdapter from 'axios-mock-adapter'
import { currentUser } from '@/mock/user'

// 注册用户相关 mock
export function registerUserMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/user/current').reply(wrap(() => currentUser))
}
```

`api/mock/modules/app.ts`：
```ts
import type MockAdapter from 'axios-mock-adapter'
import { appCards, categories } from '@/mock/app'

// 注册应用相关 mock
export function registerAppMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/app/list').reply(wrap(() => appCards))
  mock.onGet('/app/categories').reply(wrap(() => categories))
}
```

`api/mock/modules/task.ts`：
```ts
import type MockAdapter from 'axios-mock-adapter'
import { taskItems, quickTasks } from '@/mock/task'

// 注册任务相关 mock
export function registerTaskMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/task/recent').reply(wrap(() => taskItems))
  mock.onGet('/task/quick').reply(wrap(() => quickTasks))
}
```

`api/mock/modules/file.ts`：
```ts
import type MockAdapter from 'axios-mock-adapter'
import { fileItems } from '@/mock/file'

// 注册文件相关 mock
export function registerFileMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/file/recent').reply(wrap(() => fileItems))
}
```

`api/mock/modules/bid.ts`：
```ts
import type MockAdapter from 'axios-mock-adapter'
import { bidReviewSteps, riskItems, bidReviewSessions, bidDocumentExcerpt } from '@/mock/bid'

// 注册审标相关 mock
export function registerBidMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/bid/steps').reply(wrap(() => bidReviewSteps))
  mock.onGet('/bid/risks').reply(wrap(() => riskItems))
  mock.onGet('/bid/sessions').reply(wrap(() => bidReviewSessions))
  mock.onGet('/bid/document').reply(wrap(() => bidDocumentExcerpt))
}
```

`api/mock/modules/standard.ts`：
```ts
import type MockAdapter from 'axios-mock-adapter'
import { standardsResult, standardsSearchHistory, standardCategories, recommendedQuestions } from '@/mock/standard'

// 注册标准查询相关 mock
export function registerStandardMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/standard/result').reply(wrap(() => standardsResult))
  mock.onGet('/standard/history').reply(wrap(() => standardsSearchHistory))
  mock.onGet('/standard/categories').reply(wrap(() => standardCategories))
  mock.onGet('/standard/recommended').reply(wrap(() => recommendedQuestions))
}
```

`api/mock/modules/apikey.ts`：
```ts
import type MockAdapter from 'axios-mock-adapter'
import { apiKeys, modelTypes, usageByModel, usageByKey } from '@/mock/apikey'

// 注册 API Key 相关 mock
export function registerApiKeyMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/apikey/list').reply(wrap(() => apiKeys))
  mock.onGet('/apikey/models').reply(wrap(() => modelTypes))
  mock.onGet('/apikey/usage-by-model').reply(wrap(() => usageByModel))
  mock.onGet('/apikey/usage-by-key').reply(wrap(() => usageByKey))
}
```

`api/mock/modules/notification.ts`：
```ts
import type MockAdapter from 'axios-mock-adapter'
import { notifications } from '@/mock/notification'

// 注册通知相关 mock
export function registerNotificationMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/notification/list').reply(wrap(() => notifications))
}
```

- [ ] **Step 4: 创建业务接口封装（8 个文件，每个调用对应 mock 路径）**

`api/modules/user.ts`：
```ts
import request from '@/api/request'
import type { UserInfo } from '@/types'

// 获取当前用户信息
export function getCurrentUser(): Promise<UserInfo> {
  return request.get('/user/current') as unknown as Promise<UserInfo>
}
```

`api/modules/app.ts`：
```ts
import request from '@/api/request'
import type { AppCard } from '@/types'

// 获取应用列表
export function getAppList(): Promise<AppCard[]> {
  return request.get('/app/list') as unknown as Promise<AppCard[]>
}

// 获取应用分类
export function getAppCategories(): Promise<{ key: string; label: string }[]> {
  return request.get('/app/categories') as unknown as Promise<{ key: string; label: string }[]>
}
```

`api/modules/task.ts`：
```ts
import request from '@/api/request'
import type { TaskItem } from '@/types'

// 获取最近任务
export function getRecentTasks(): Promise<TaskItem[]> {
  return request.get('/task/recent') as unknown as Promise<TaskItem[]>
}

// 获取快捷任务
export function getQuickTasks(): Promise<{ id: string; title: string; tag: string; route: string; icon: string }[]> {
  return request.get('/task/quick') as unknown as Promise<{ id: string; title: string; tag: string; route: string; icon: string }[]>
}
```

`api/modules/file.ts`：
```ts
import request from '@/api/request'
import type { FileItem } from '@/types'

// 获取最近文件
export function getRecentFiles(): Promise<FileItem[]> {
  return request.get('/file/recent') as unknown as Promise<FileItem[]>
}
```

`api/modules/bid.ts`：
```ts
import request from '@/api/request'
import type { BidReviewStep, RiskItem, BidReviewSession } from '@/types'

// 获取审标步骤
export function getBidSteps(): Promise<BidReviewStep[]> {
  return request.get('/bid/steps') as unknown as Promise<BidReviewStep[]>
}

// 获取风险项
export function getBidRisks(): Promise<RiskItem[]> {
  return request.get('/bid/risks') as unknown as Promise<RiskItem[]>
}

// 获取审标会话历史
export function getBidSessions(): Promise<BidReviewSession[]> {
  return request.get('/bid/sessions') as unknown as Promise<BidReviewSession[]>
}

// 获取文档原文
export function getBidDocument(): Promise<string> {
  return request.get('/bid/document') as unknown as Promise<string>
}
```

`api/modules/standard.ts`：
```ts
import request from '@/api/request'
import type { StandardResult, StandardSearchHistory, StandardCategory } from '@/types'

// 获取标准查询结果
export function getStandardResult(): Promise<StandardResult[]> {
  return request.get('/standard/result') as unknown as Promise<StandardResult[]>
}

// 获取查询历史
export function getStandardHistory(): Promise<StandardSearchHistory[]> {
  return request.get('/standard/history') as unknown as Promise<StandardSearchHistory[]>
}

// 获取标准分类
export function getStandardCategories(): Promise<StandardCategory[]> {
  return request.get('/standard/categories') as unknown as Promise<StandardCategory[]>
}

// 获取推荐问题
export function getRecommendedQuestions(): Promise<string[]> {
  return request.get('/standard/recommended') as unknown as Promise<string[]>
}
```

`api/modules/apikey.ts`：
```ts
import request from '@/api/request'
import type { ApiKey, ModelType, UsageByModel, UsageByKey } from '@/types'

// 获取 API Key 列表
export function getApiKeyList(): Promise<ApiKey[]> {
  return request.get('/apikey/list') as unknown as Promise<ApiKey[]>
}

// 获取模型类型
export function getModelTypes(): Promise<ModelType[]> {
  return request.get('/apikey/models') as unknown as Promise<ModelType[]>
}

// 获取按模型用量
export function getUsageByModel(): Promise<UsageByModel[]> {
  return request.get('/apikey/usage-by-model') as unknown as Promise<UsageByModel[]>
}

// 获取按 Key 用量
export function getUsageByKey(): Promise<UsageByKey[]> {
  return request.get('/apikey/usage-by-key') as unknown as Promise<UsageByKey[]>
}
```

`api/modules/notification.ts`：
```ts
import request from '@/api/request'
import type { Notification } from '@/types'

// 获取通知列表
export function getNotifications(): Promise<Notification[]> {
  return request.get('/notification/list') as unknown as Promise<Notification[]>
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
git add user-web/src/api/ user-web/src/main.ts
git commit -m "feat(user-web): setup axios + mock-adapter with module APIs"
```

---

## Task 7: user-web Pinia store

**Files:**
- Create: `d:\AI\DredgeAI\user-web\src\stores\user.ts`
- Create: `d:\AI\DredgeAI\user-web\src\stores\app.ts`

- [ ] **Step 1: 创建 stores/user.ts**

```ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { UserInfo, Notification } from '@/types'
import { getCurrentUser } from '@/api/modules/user'
import { getNotifications } from '@/api/modules/notification'

// 用户状态：当前用户信息 + 通知
export const useUserStore = defineStore('user', () => {
  const userInfo = ref<UserInfo | null>(null)
  const notifications = ref<Notification[]>([])
  const unreadCount = ref(0)

  // 拉取当前用户
  async function fetchUser(): Promise<void> {
    userInfo.value = await getCurrentUser()
  }

  // 拉取通知
  async function fetchNotifications(): Promise<void> {
    notifications.value = await getNotifications()
    unreadCount.value = notifications.value.filter((n) => !n.read).length
  }

  return { userInfo, notifications, unreadCount, fetchUser, fetchNotifications }
}, {
  persist: { pick: ['userInfo'] },
})
```

- [ ] **Step 2: 创建 stores/app.ts**

```ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { AppCard, TaskItem, FileItem } from '@/types'
import { getAppList, getAppCategories } from '@/api/modules/app'
import { getRecentTasks, getQuickTasks } from '@/api/modules/task'
import { getRecentFiles } from '@/api/modules/file'

// 应用全局状态：应用列表、任务、文件
export const useAppStore = defineStore('app', () => {
  const apps = ref<AppCard[]>([])
  const categories = ref<{ key: string; label: string }[]>([])
  const tasks = ref<TaskItem[]>([])
  const quickTasks = ref<{ id: string; title: string; tag: string; route: string; icon: string }[]>([])
  const files = ref<FileItem[]>([])
  const sidebarCollapsed = ref(false)

  async function fetchApps(): Promise<void> { apps.value = await getAppList() }
  async function fetchCategories(): Promise<void> { categories.value = await getAppCategories() }
  async function fetchTasks(): Promise<void> { tasks.value = await getRecentTasks() }
  async function fetchQuickTasks(): Promise<void> { quickTasks.value = await getQuickTasks() }
  async function fetchFiles(): Promise<void> { files.value = await getRecentFiles() }
  function toggleSidebar(): void { sidebarCollapsed.value = !sidebarCollapsed.value }

  return {
    apps, categories, tasks, quickTasks, files, sidebarCollapsed,
    fetchApps, fetchCategories, fetchTasks, fetchQuickTasks, fetchFiles, toggleSidebar,
  }
}, {
  persist: { pick: ['sidebarCollapsed'] },
})
```

- [ ] **Step 3: 提交**

```bash
git add user-web/src/stores/
git commit -m "feat(user-web): add pinia stores with persistedstate"
```

---

## Task 8: user-web 通用组件

**Files:**
- Create: `d:\AI\DredgeAI\user-web\src\components\PageHeader.vue`
- Create: `d:\AI\DredgeAI\user-web\src\components\SectionCard.vue`
- Create: `d:\AI\DredgeAI\user-web\src\components\MetricCard.vue`
- Create: `d:\AI\DredgeAI\user-web\src\components\StatusTag.vue`
- Create: `d:\AI\DredgeAI\user-web\src\components\SearchInput.vue`
- Create: `d:\AI\DredgeAI\user-web\src\components\ChartContainer.vue`
- Create: `d:\AI\DredgeAI\user-web\src\components\DataSkeleton.vue`
- Create: `d:\AI\DredgeAI\user-web\src\components\EmptyState.vue`

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

- [ ] **Step 3: 创建 MetricCard.vue**

```vue
<template>
  <div class="metric-card">
    <div class="metric-label">{{ title }}</div>
    <div class="metric-value">{{ value }}</div>
    <div v-if="trend" class="metric-trend" :class="trendUp ? 'up' : 'down'">
      <component :is="trendUp ? ArrowUpOutlined : ArrowDownOutlined" />
      <span>{{ trend }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ArrowUpOutlined, ArrowDownOutlined } from '@ant-design/icons-vue'

// 指标卡：标题、数值、趋势
defineProps<{
  title: string
  value: string | number
  trend?: string
  trendUp?: boolean
}>()
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
.metric-label {
  font-size: @font-size-sm;
  color: @text-secondary;
  margin-bottom: @spacing-sm;
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
  gap: @spacing-xs;
  font-size: @font-size-xs;
  margin-top: @spacing-sm;
  &.up { color: @success; }
  &.down { color: @danger; }
}
</style>
```

- [ ] **Step 4: 创建 StatusTag.vue**

```vue
<template>
  <a-tag :color="colorMap[status] || 'default'">{{ status }}</a-tag>
</template>

<script setup lang="ts">
// 统一状态标签：颜色映射
const colorMap: Record<string, string> = {
  '已授权': 'green',
  '已上架': 'green',
  '启用': 'green',
  '已完成': 'green',
  '已通过': 'green',
  '进行中': 'blue',
  '待审核': 'orange',
  '待申请': 'orange',
  '已暂停': 'orange',
  '已下架': 'red',
  '已禁用': 'red',
  '已拒绝': 'red',
  '已失败': 'red',
  '高风险': 'red',
  '中风险': 'orange',
  '低风险': 'blue',
}

defineProps<{ status: string }>()
</script>
```

- [ ] **Step 5: 创建 SearchInput.vue**

```vue
<template>
  <a-input
    :value="modelValue"
    :placeholder="placeholder"
    allow-clear
    class="search-input"
    @update:value="$emit('update:modelValue', $event)"
    @press-enter="$emit('search', $event)"
  >
    <template #prefix>
      <SearchOutlined class="search-icon" />
    </template>
  </a-input>
</template>

<script setup lang="ts">
import { SearchOutlined } from '@ant-design/icons-vue'

// 带搜索图标的输入框
defineProps<{ modelValue: string; placeholder?: string }>()
defineEmits<{ 'update:modelValue': [string]; search: [unknown] }>()
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.search-input {
  border-radius: @radius-base;
}
.search-icon {
  color: @text-tertiary;
}
</style>
```

- [ ] **Step 6: 创建 ChartContainer.vue**

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

- [ ] **Step 7: 创建 DataSkeleton.vue**

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

- [ ] **Step 8: 创建 EmptyState.vue**

```vue
<template>
  <div class="empty-state">
    <a-empty :description="description" />
  </div>
</template>

<script setup lang="ts">
// 空状态
withDefaults(defineProps<{ description?: string }>(), { description: '暂无数据' })
</script>

<style scoped lang="less">
.empty-state {
  padding: 48px 0;
}
</style>
```

- [ ] **Step 9: 提交**

```bash
git add user-web/src/components/
git commit -m "feat(user-web): add shared UI components"
```

---

## Task 9: user-web 布局与路由

**Files:**
- Create: `d:\AI\DredgeAI\user-web\src\layouts\UserLayout.vue`
- Create: `d:\AI\DredgeAI\user-web\src\router\index.ts`

- [ ] **Step 1: 创建 UserLayout.vue（侧边栏 + 头部 + 内容区）**

```vue
<template>
  <a-layout class="user-layout">
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
        <div class="logo-mark">智</div>
        <div v-if="!collapsed" class="logo-text">
          <div class="logo-name">智浚 AI</div>
          <div class="logo-sub">DredgeAI</div>
        </div>
      </div>
      <a-menu
        v-model:selectedKeys="selectedKeys"
        theme="dark"
        mode="inline"
        class="sider-menu"
        @click="handleMenuClick"
      >
        <a-menu-item key="/dashboard">
          <DashboardOutlined />
          <span>工作台</span>
        </a-menu-item>
        <a-menu-item key="/apps">
          <AppstoreOutlined />
          <span>应用广场</span>
        </a-menu-item>
        <a-menu-item key="/bid-review">
          <FileSearchOutlined />
          <span>AI 审标</span>
        </a-menu-item>
        <a-menu-item key="/standards">
          <BookOutlined />
          <span>标准查询</span>
        </a-menu-item>
        <a-menu-item key="/profile">
          <UserOutlined />
          <span>个人中心</span>
        </a-menu-item>
        <a-menu-item key="/api">
          <ApiOutlined />
          <span>API 管理</span>
        </a-menu-item>
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
            placeholder="搜索应用、任务、标准..."
            class="header-search"
            @search="handleGlobalSearch"
          />
        </div>
        <div class="header-right">
          <a-badge :count="userStore.unreadCount" :offset="[2, -2]">
            <BellOutlined class="header-icon" @click="showNotifications = true" />
          </a-badge>
          <a-tooltip title="管理后台">
            <SettingOutlined class="header-icon" @click="goAdmin" />
          </a-tooltip>
          <a-dropdown>
            <span class="user-info">
              <a-avatar :style="{ background: '@{brand-gradient}' }">
                {{ userStore.userInfo?.name?.[0] || 'U' }}
              </a-avatar>
              <span class="user-name">{{ userStore.userInfo?.name || '用户' }}</span>
            </span>
            <template #overlay>
              <a-menu @click="handleUserMenu">
                <a-menu-item key="profile"><UserOutlined /> 个人中心</a-menu-item>
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

    <a-drawer
      v-model:open="showNotifications"
      title="通知中心"
      placement="right"
      width="380"
    >
      <a-list :data-source="userStore.notifications" item-layout="vertical">
        <template #renderItem="{ item }">
          <a-list-item>
            <a-list-item-meta>
              <template #title>
                <a-tag :color="notifColorMap[item.type]" size="small">{{ notifLabelMap[item.type] }}</a-tag>
                <span class="notif-title">{{ item.title }}</span>
              </template>
              <template #description>{{ item.content }}</template>
            </a-list-item-meta>
            <div class="notif-time">{{ item.time }}</div>
          </a-list-item>
        </template>
      </a-list>
    </a-drawer>
  </a-layout>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  DashboardOutlined, AppstoreOutlined, FileSearchOutlined, BookOutlined,
  UserOutlined, ApiOutlined, MenuFoldOutlined, MenuUnfoldOutlined,
  BellOutlined, SettingOutlined, LogoutOutlined,
} from '@ant-design/icons-vue'
import { useUserStore } from '@/stores/user'
import { ADMIN_WEB_URL } from '@/utils/constants'
import type { Notification } from '@/types'

const router = useRouter()
const route = useRoute()
const userStore = useUserStore()

const collapsed = ref(false)
const selectedKeys = ref<string[]>([route.path])
const showNotifications = ref(false)

const notifColorMap: Record<Notification['type'], string> = {
  system: 'blue',
  business: 'green',
  audit: 'orange',
}
const notifLabelMap: Record<Notification['type'], string> = {
  system: '系统',
  business: '业务',
  audit: '审计',
}

onMounted(() => {
  if (!userStore.userInfo) userStore.fetchUser()
  userStore.fetchNotifications()
})

watch(() => route.path, (p) => { selectedKeys.value = [p] })

function handleMenuClick({ key }: { key: string }): void {
  router.push(key)
}

function handleGlobalSearch(value: string): void {
  if (!value) return
  router.push({ path: '/apps', query: { q: value } })
}

function goAdmin(): void {
  window.location.href = ADMIN_WEB_URL
}

function handleUserMenu({ key }: { key: string }): void {
  if (key === 'profile') router.push('/profile')
  else if (key === 'logout') {
    localStorage.removeItem('DREDGE_AI_TOKEN')
    router.push('/dashboard')
  }
}
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.user-layout { height: 100vh; }

.sider {
  background: @sidebar-bg !important;
  :deep(.ant-layout-sider-children) { display: flex; flex-direction: column; }
}

.logo {
  height: @header-height;
  display: flex;
  align-items: center;
  gap: @spacing-md;
  padding: 0 @spacing-xl;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
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

.sider-menu {
  flex: 1;
  border-right: none !important;
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
.user-name {
  font-size: @font-size-sm;
  color: @text-primary;
}

.content {
  flex: 1;
  overflow-y: auto;
  background: @content-bg;
}

.notif-title { margin-left: 8px; font-weight: 500; }
.notif-time { font-size: 12px; color: @text-tertiary; margin-top: 4px; }
</style>
```

- [ ] **Step 2: 创建 router/index.ts**

```ts
import { createRouter, createWebHistory } from 'vue-router'
import UserLayout from '@/layouts/UserLayout.vue'

// user-web 路由：所有页面懒加载，根路径重定向到 dashboard
const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      component: UserLayout,
      redirect: '/dashboard',
      children: [
        { path: 'dashboard', name: 'UserDashboard', component: () => import('@/views/dashboard/index.vue'), meta: { title: '工作台' } },
        { path: 'apps', name: 'UserApps', component: () => import('@/views/apps/index.vue'), meta: { title: '应用广场' } },
        { path: 'bid-review', name: 'BidReview', component: () => import('@/views/bid-review/index.vue'), meta: { title: 'AI 审标' } },
        { path: 'standards', name: 'Standards', component: () => import('@/views/standards/index.vue'), meta: { title: '标准查询' } },
        { path: 'profile', name: 'Profile', component: () => import('@/views/profile/index.vue'), meta: { title: '个人中心' } },
        { path: 'api', name: 'ApiManage', component: () => import('@/views/api/index.vue'), meta: { title: 'API 管理' } },
      ],
    },
  ],
})

// 路由标题
router.afterEach((to) => {
  const title = to.meta.title as string | undefined
  document.title = title ? `${title} · 智浚 AI` : '智浚 AI · 用户端'
})

export default router
```

- [ ] **Step 3: 提交**

```bash
git add user-web/src/layouts/ user-web/src/router/
git commit -m "feat(user-web): add layout and router"
```

---

## Task 10: user-web Dashboard 工作台页面

**Files:**
- Create: `d:\AI\DredgeAI\user-web\src\views\dashboard\index.vue`

- [ ] **Step 1: 创建 Dashboard 页面**

```vue
<template>
  <div class="page-container">
    <!-- 欢迎区 -->
    <div class="welcome-banner">
      <div class="welcome-left">
        <h1 class="welcome-title">
          你好，{{ userStore.userInfo?.name || '用户' }} 👋
        </h1>
        <p class="welcome-desc">
          {{ userStore.userInfo?.position }} · {{ userStore.userInfo?.department }} · 今天有 {{ pendingTaskCount }} 个任务待处理
        </p>
        <div class="welcome-tags">
          <a-tag v-for="scope in (userStore.userInfo?.authorizedScopes || []).slice(0, 4)" :key="scope" color="cyan">
            {{ scope }}
          </a-tag>
        </div>
      </div>
      <div class="welcome-right">
        <div class="quick-task-grid">
          <div
            v-for="task in appStore.quickTasks"
            :key="task.id"
            class="quick-task-card"
            @click="router.push(task.route)"
          >
            <component :is="iconMap[task.icon]" class="quick-task-icon" />
            <div class="quick-task-info">
              <div class="quick-task-title">{{ task.title }}</div>
              <div class="quick-task-tag">{{ task.tag }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 主区双栏 -->
    <a-row :gutter="[24, 24]" class="main-row">
      <a-col :span="16">
        <!-- 推荐任务 -->
        <SectionCard title="最近任务" class="mb-24">
          <template #extra>
            <a-button type="link" size="small" @click="router.push('/apps')">查看全部</a-button>
          </template>
          <a-list :data-source="appStore.tasks" :loading="loading">
            <template #renderItem="{ item }">
              <a-list-item class="task-item">
                <a-list-item-meta>
                  <template #title>
                    <span class="task-title" @click="router.push('/bid-review')">{{ item.title }}</span>
                  </template>
                  <template #description>
                    <span class="task-meta">{{ item.app }} · 更新于 {{ item.updatedAt }}</span>
                  </template>
                  <template #avatar>
                    <div class="task-avatar" :class="`task-avatar--${item.status}`">
                      <component :is="statusIconMap[item.status]" />
                    </div>
                  </template>
                </a-list-item-meta>
                <div class="task-right">
                  <StatusTag :status="item.status" />
                  <a-progress v-if="item.progress !== undefined" :percent="item.progress" :size="'small'" class="task-progress" />
                </div>
              </a-list-item>
            </template>
          </a-list>
        </SectionCard>

        <!-- 个人效率趋势 -->
        <SectionCard title="本周效率趋势">
          <ChartContainer :option="efficiencyChartOption" height="280px" :loading="chartLoading" />
        </SectionCard>
      </a-col>

      <a-col :span="8">
        <!-- 授权应用 -->
        <SectionCard title="授权应用" class="mb-24">
          <template #extra>
            <a-button type="link" size="small" @click="router.push('/apps')">应用广场</a-button>
          </template>
          <div class="app-list">
            <div
              v-for="app in authorizedApps"
              :key="app.id"
              class="app-item"
              @click="app.route && router.push(app.route)"
            >
              <div class="app-icon-wrap">
                <component :is="iconMap[app.icon]" />
              </div>
              <div class="app-info">
                <div class="app-name">{{ app.title }}</div>
                <div class="app-desc">{{ app.description }}</div>
              </div>
            </div>
          </div>
        </SectionCard>

        <!-- 最近文件 -->
        <SectionCard title="最近文件">
          <a-list :data-source="appStore.files" :loading="loading" size="small">
            <template #renderItem="{ item }">
              <a-list-item class="file-item">
                <a-list-item-meta>
                  <template #title>
                    <span class="file-name">{{ item.name }}</span>
                  </template>
                  <template #description>
                    <span class="file-meta">{{ item.size }} · {{ item.updatedAt }}</span>
                  </template>
                  <template #avatar>
                    <div class="file-icon" :class="`file-icon--${item.type}`">
                      <component :is="fileIconMap[item.type]" />
                    </div>
                  </template>
                </a-list-item-meta>
              </a-list-item>
            </template>
          </a-list>
        </SectionCard>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import {
  FileSearchOutlined, BookOutlined, EditOutlined, SafetyOutlined,
  DashboardOutlined, ApiOutlined, QuestionCircleOutlined, SwapOutlined,
  CodeOutlined, TeamOutlined, FilePdfOutlined, FileWordOutlined,
  FileExcelOutlined, FileImageOutlined, FileOutlined,
  CheckCircleOutlined, SyncOutlined, PauseCircleOutlined, CloseCircleOutlined,
} from '@ant-design/icons-vue'
import SectionCard from '@/components/SectionCard.vue'
import StatusTag from '@/components/StatusTag.vue'
import ChartContainer from '@/components/ChartContainer.vue'
import { useUserStore } from '@/stores/user'
import { useAppStore } from '@/stores/app'
import { efficiencyTrend } from '@/mock/chart'
import type { Component } from 'vue'

const router = useRouter()
const userStore = useUserStore()
const appStore = useAppStore()

const loading = ref(false)
const chartLoading = ref(false)

// 图标映射
const iconMap: Record<string, Component> = {
  FileSearchOutlined, BookOutlined, EditOutlined, SafetyOutlined,
  DashboardOutlined, ApiOutlined, QuestionCircleOutlined, SwapOutlined,
  CodeOutlined, TeamOutlined,
}
const statusIconMap: Record<string, Component> = {
  '已完成': CheckCircleOutlined,
  '进行中': SyncOutlined,
  '已暂停': PauseCircleOutlined,
  '已失败': CloseCircleOutlined,
}
const fileIconMap: Record<string, Component> = {
  pdf: FilePdfOutlined,
  docx: FileWordOutlined,
  xlsx: FileExcelOutlined,
  image: FileImageOutlined,
  other: FileOutlined,
}

// 已授权应用
const authorizedApps = computed(() => appStore.apps.filter((a) => a.status === '已授权').slice(0, 5))

// 待处理任务数
const pendingTaskCount = computed(() => appStore.tasks.filter((t) => t.status === '进行中' || t.status === '已暂停').length)

// 效率趋势图配置
const efficiencyChartOption = computed(() => ({
  tooltip: { trigger: 'axis' },
  legend: { data: ['任务数', '完成数'], bottom: 0 },
  grid: { left: '3%', right: '4%', bottom: '10%', top: '5%', containLabel: true },
  xAxis: { type: 'category', boundaryGap: false, data: efficiencyTrend.categories },
  yAxis: { type: 'value' },
  series: efficiencyTrend.series.map((s, i) => ({
    name: s.name,
    type: 'line',
    smooth: true,
    data: s.data,
    itemStyle: { color: i === 0 ? '#0EA5E9' : '#10B981' },
    areaStyle: { opacity: 0.1 },
  })),
}))

onMounted(async () => {
  loading.value = true
  chartLoading.value = true
  await Promise.all([
    appStore.fetchTasks(),
    appStore.fetchQuickTasks(),
    appStore.fetchFiles(),
    appStore.fetchApps(),
  ])
  loading.value = false
  chartLoading.value = false
})
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.mb-24 { margin-bottom: @spacing-xl; }

.welcome-banner {
  background: @brand-gradient;
  border-radius: @radius-xl;
  padding: @spacing-2xl;
  margin-bottom: @spacing-xl;
  display: flex;
  align-items: center;
  justify-content: space-between;
  color: white;
  box-shadow: @shadow-brand;
}
.welcome-title {
  font-size: @font-size-3xl;
  font-weight: @font-weight-bold;
  margin-bottom: @spacing-sm;
}
.welcome-desc {
  font-size: @font-size-base;
  opacity: 0.9;
  margin-bottom: @spacing-md;
}
.welcome-tags { display: flex; gap: @spacing-xs; flex-wrap: wrap; }

.quick-task-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: @spacing-md;
  min-width: 360px;
}
.quick-task-card {
  background: rgba(255, 255, 255, 0.15);
  backdrop-filter: blur(8px);
  border-radius: @radius-base;
  padding: @spacing-md @spacing-base;
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  cursor: pointer;
  transition: background @transition-base;
  &:hover { background: rgba(255, 255, 255, 0.25); }
}
.quick-task-icon { font-size: 24px; color: white; }
.quick-task-title { font-size: @font-size-sm; font-weight: @font-weight-medium; color: white; }
.quick-task-tag { font-size: 10px; opacity: 0.8; color: white; }

.task-item {
  padding: @spacing-md 0 !important;
  &:hover .task-title { color: @brand-primary; }
}
.task-title { cursor: pointer; font-weight: @font-weight-medium; transition: color @transition-base; }
.task-meta { font-size: @font-size-xs; color: @text-tertiary; }
.task-avatar {
  width: 40px; height: 40px;
  border-radius: @radius-base;
  display: flex; align-items: center; justify-content: center;
  font-size: 18px;
  &--已完成 { background: fade(@success, 12%); color: @success; }
  &--进行中 { background: fade(@brand-primary, 12%); color: @brand-primary; }
  &--已暂停 { background: fade(@warning, 12%); color: @warning; }
  &--已失败 { background: fade(@danger, 12%); color: @danger; }
}
.task-right { display: flex; flex-direction: column; align-items: flex-end; gap: @spacing-xs; }
.task-progress { width: 120px; }

.app-list { display: flex; flex-direction: column; gap: @spacing-sm; }
.app-item {
  display: flex; align-items: center; gap: @spacing-md;
  padding: @spacing-sm;
  border-radius: @radius-base;
  cursor: pointer;
  transition: background @transition-base;
  &:hover { background: @content-bg; }
}
.app-icon-wrap {
  width: 36px; height: 36px;
  border-radius: @radius-base;
  background: fade(@brand-primary, 10%);
  color: @brand-primary;
  display: flex; align-items: center; justify-content: center;
  font-size: 16px;
  flex-shrink: 0;
}
.app-name { font-size: @font-size-sm; font-weight: @font-weight-medium; color: @text-primary; }
.app-desc { font-size: @font-size-xs; color: @text-tertiary; .truncate-1(); }

.file-item { padding: @spacing-sm 0 !important; }
.file-name { font-size: @font-size-sm; color: @text-primary; }
.file-meta { font-size: @font-size-xs; color: @text-tertiary; }
.file-icon {
  width: 32px; height: 32px;
  border-radius: @radius-sm;
  display: flex; align-items: center; justify-content: center;
  font-size: 16px;
  &--pdf { background: fade(@danger, 12%); color: @danger; }
  &--docx { background: fade(@info, 12%); color: @info; }
  &--xlsx { background: fade(@success, 12%); color: @success; }
  &--image { background: fade(@warning, 12%); color: @warning; }
  &--other { background: @divider-color; color: @text-secondary; }
}

.truncate-1() {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
```

- [ ] **Step 2: 提交**

```bash
git add user-web/src/views/dashboard/
git commit -m "feat(user-web): add dashboard workbench page"
```

---

## Task 11: user-web Apps 应用广场页面

**Files:**
- Create: `d:\AI\DredgeAI\user-web\src\views\apps\index.vue`

- [ ] **Step 1: 创建 Apps 页面**

```vue
<template>
  <div class="page-container">
    <PageHeader title="应用广场" description="按场景筛选 AI 应用，快速进入工作流">
      <template #extra>
        <SearchInput v-model="searchKeyword" placeholder="搜索应用名称" class="app-search" />
      </template>
    </PageHeader>

    <!-- 场景筛选 -->
    <div class="filter-bar">
      <a-segmented v-model:value="activeCategory" :options="categoryOptions" @change="handleCategoryChange" />
    </div>

    <!-- 应用卡片网格 -->
    <a-row :gutter="[20, 20]">
      <a-col v-for="app in filteredApps" :key="app.id" :span="8">
        <div class="app-card" :class="{ 'app-card--disabled': app.status === '待申请' }">
          <div class="app-card-header">
            <div class="app-card-icon">
              <component :is="iconMap[app.icon]" />
            </div>
            <div class="app-card-status">
              <StatusTag :status="app.status" />
            </div>
          </div>
          <div class="app-card-body">
            <h3 class="app-card-title">{{ app.title }}</h3>
            <p class="app-card-desc">{{ app.description }}</p>
            <div class="app-card-meta">
              <a-tag size="small">{{ app.category }}</a-tag>
              <span v-if="app.version" class="app-card-version">{{ app.version }}</span>
            </div>
          </div>
          <div class="app-card-footer">
            <a-button
              v-if="app.status === '已授权'"
              type="primary"
              block
              @click="handleEnterApp(app)"
            >
              进入应用
            </a-button>
            <a-button
              v-else-if="app.status === '待申请'"
              block
              @click="handleApplyApp(app)"
            >
              申请权限
            </a-button>
            <a-button v-else block disabled>已下架</a-button>
          </div>
        </div>
      </a-col>
    </a-row>

    <EmptyState v-if="filteredApps.length === 0" description="没有匹配的应用" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  FileSearchOutlined, BookOutlined, EditOutlined, SafetyOutlined,
  DashboardOutlined, ApiOutlined, QuestionCircleOutlined, SwapOutlined,
  CodeOutlined, TeamOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import SearchInput from '@/components/SearchInput.vue'
import StatusTag from '@/components/StatusTag.vue'
import EmptyState from '@/components/EmptyState.vue'
import { useAppStore } from '@/stores/app'
import type { AppCard, Component } from '@/types'

const router = useRouter()
const appStore = useAppStore()

const searchKeyword = ref('')
const activeCategory = ref('all')

const iconMap: Record<string, Component> = {
  FileSearchOutlined, BookOutlined, EditOutlined, SafetyOutlined,
  DashboardOutlined, ApiOutlined, QuestionCircleOutlined, SwapOutlined,
  CodeOutlined, TeamOutlined,
}

const categoryOptions = computed(() => {
  const opts = [{ label: '全部', value: 'all' }]
  return opts.concat((appStore.categories as { key: string; label: string }[])
    .filter((c) => c.key !== 'all')
    .map((c) => ({ label: c.label, value: c.key })))
})

const filteredApps = computed(() => {
  let list = appStore.apps
  if (activeCategory.value !== 'all') {
    list = list.filter((a) => a.category === activeCategory.value)
  }
  if (searchKeyword.value) {
    const kw = searchKeyword.value.toLowerCase()
    list = list.filter((a) => a.title.toLowerCase().includes(kw) || a.description.toLowerCase().includes(kw))
  }
  return list
})

function handleCategoryChange(): void { /* 响应式自动处理 */ }

function handleEnterApp(app: AppCard): void {
  if (app.route) {
    router.push(app.route)
  } else {
    message.info(`${app.title} 暂未开放`)
  }
}

function handleApplyApp(app: AppCard): void {
  message.success(`已提交 ${app.title} 的权限申请`)
}

onMounted(async () => {
  await Promise.all([appStore.fetchApps(), appStore.fetchCategories()])
})
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.app-search { width: 280px; }

.filter-bar {
  margin-bottom: @spacing-xl;
}

.app-card {
  background: @card-bg;
  border-radius: @radius-lg;
  border: 1px solid @border-color;
  padding: @spacing-xl;
  transition: all @transition-base;
  height: 100%;
  display: flex;
  flex-direction: column;
  &:hover {
    box-shadow: @shadow-md;
    transform: translateY(-2px);
    border-color: fade(@brand-primary, 30%);
  }
  &--disabled { opacity: 0.7; }
}
.app-card-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: @spacing-md;
}
.app-card-icon {
  width: 48px; height: 48px;
  border-radius: @radius-base;
  background: @brand-gradient;
  color: white;
  display: flex; align-items: center; justify-content: center;
  font-size: 22px;
  box-shadow: @shadow-brand;
}
.app-card-title {
  font-size: @font-size-lg;
  font-weight: @font-weight-semibold;
  color: @text-primary;
  margin-bottom: @spacing-xs;
}
.app-card-desc {
  font-size: @font-size-sm;
  color: @text-secondary;
  line-height: 1.5;
  margin-bottom: @spacing-md;
  min-height: 42px;
}
.app-card-meta {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  margin-bottom: @spacing-lg;
}
.app-card-version {
  font-size: @font-size-xs;
  color: @text-tertiary;
}
.app-card-footer { margin-top: auto; }
</style>
```

- [ ] **Step 2: 提交**

```bash
git add user-web/src/views/apps/
git commit -m "feat(user-web): add apps gallery page"
```

---

## Task 12: user-web BidReview AI 审标页面

**Files:**
- Create: `d:\AI\DredgeAI\user-web\src\views\bid-review\index.vue`

- [ ] **Step 1: 创建 BidReview 页面（三栏布局）**

```vue
<template>
  <div class="page-container bid-review">
    <a-row :gutter="[16, 16]">
      <!-- 左栏：步骤流 + 历史 -->
      <a-col :span="6">
        <SectionCard title="审标步骤" class="mb-16">
          <a-steps :current="currentStep" direction="vertical" size="small">
            <a-step
              v-for="(step, i) in steps"
              :key="i"
              :title="step.title"
              :description="step.description"
              :status="step.status"
            />
          </a-steps>
        </SectionCard>

        <SectionCard title="历史会话" nopad>
          <div class="session-list">
            <div
              v-for="session in sessions"
              :key="session.id"
              class="session-item"
              :class="{ active: session.id === activeSessionId }"
              @click="activeSessionId = session.id"
            >
              <div class="session-name">{{ session.document }}</div>
              <div class="session-meta">
                <span>{{ session.date }}</span>
                <a-badge :count="session.riskCount" :number-style="{ backgroundColor: session.riskCount > 0 ? '#EF4444' : '#10B981' }" />
              </div>
            </div>
          </div>
        </SectionCard>
      </a-col>

      <!-- 中栏：文档预览 + 追问 -->
      <a-col :span="12">
        <SectionCard title="文档预览" class="mb-16">
          <template #extra>
            <a-button type="link" size="small">
              <upload-outlined />
              重新上传
            </a-button>
          </template>
          <div class="doc-viewer">
            <pre class="doc-content">{{ document }}</pre>
          </div>
        </SectionCard>

        <SectionCard title="追问与对话" nopad>
          <div class="chat-area">
            <div class="chat-messages">
              <div
                v-for="(msg, i) in currentSession?.snippets || []"
                :key="i"
                class="chat-msg"
                :class="`chat-msg--${msg.role}`"
              >
                <div class="chat-avatar">{{ msg.role === 'user' ? '我' : 'AI' }}</div>
                <div class="chat-bubble">{{ msg.content }}</div>
              </div>
            </div>
            <div class="chat-input">
              <a-input-search
                v-model:value="questionInput"
                placeholder="输入追问内容..."
                enter-button="发送"
                @search="handleSendQuestion"
              />
            </div>
          </div>
        </SectionCard>
      </a-col>

      <!-- 右栏：风险面板 -->
      <a-col :span="6">
        <SectionCard title="风险清单" class="mb-16">
          <template #extra>
            <a-button type="link" size="small">
              <download-outlined />
              导出报告
            </a-button>
          </template>
          <div class="risk-summary">
            <div class="risk-stat" v-for="level in riskSummary" :key="level.label">
              <div class="risk-stat-num" :class="`risk-stat-num--${level.key}`">{{ level.count }}</div>
              <div class="risk-stat-label">{{ level.label }}</div>
            </div>
          </div>
        </SectionCard>

        <div class="risk-list">
          <div
            v-for="risk in risks"
            :key="risk.id"
            class="risk-card"
            :class="`risk-card--${risk.level}`"
          >
            <div class="risk-card-header">
              <StatusTag :status="risk.level" />
              <span class="risk-source">{{ risk.source }}</span>
            </div>
            <div class="risk-content">{{ risk.content }}</div>
            <div v-if="risk.suggestion" class="risk-suggestion">
              <bulb-outlined />
              <span>{{ risk.suggestion }}</span>
            </div>
          </div>
        </div>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { UploadOutlined, DownloadOutlined, BulbOutlined } from '@ant-design/icons-vue'
import SectionCard from '@/components/SectionCard.vue'
import StatusTag from '@/components/StatusTag.vue'
import { getBidSteps, getBidRisks, getBidSessions, getBidDocument } from '@/api/modules/bid'
import type { BidReviewStep, RiskItem, BidReviewSession } from '@/types'

const steps = ref<BidReviewStep[]>([])
const risks = ref<RiskItem[]>([])
const sessions = ref<BidReviewSession[]>([])
const document = ref('')
const activeSessionId = ref('')
const questionInput = ref('')

const currentStep = computed(() => steps.value.findIndex((s) => s.status === 'process'))

const currentSession = computed(() => sessions.value.find((s) => s.id === activeSessionId.value))

const riskSummary = computed(() => [
  { key: 'high', label: '高风险', count: risks.value.filter((r) => r.level === '高风险').length },
  { key: 'mid', label: '中风险', count: risks.value.filter((r) => r.level === '中风险').length },
  { key: 'low', label: '低风险', count: risks.value.filter((r) => r.level === '低风险').length },
])

function handleSendQuestion(): void {
  if (!questionInput.value.trim()) return
  message.success('已发送追问，AI 正在分析...')
  questionInput.value = ''
}

onMounted(async () => {
  const [s, r, sess, doc] = await Promise.all([
    getBidSteps(), getBidRisks(), getBidSessions(), getBidDocument(),
  ])
  steps.value = s
  risks.value = r
  sessions.value = sess
  document.value = doc
  if (sess.length > 0) activeSessionId.value = sess[0].id
})
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.mb-16 { margin-bottom: @spacing-lg; }

.session-list { max-height: 400px; overflow-y: auto; }
.session-item {
  padding: @spacing-md @spacing-xl;
  border-bottom: 1px solid @divider-color;
  cursor: pointer;
  transition: background @transition-base;
  &:hover { background: @content-bg; }
  &.active { background: fade(@brand-primary, 6%); border-left: 3px solid @brand-primary; }
}
.session-name { font-size: @font-size-sm; font-weight: @font-weight-medium; color: @text-primary; margin-bottom: 4px; }
.session-meta {
  display: flex; align-items: center; justify-content: space-between;
  font-size: @font-size-xs; color: @text-tertiary;
}

.doc-viewer {
  background: @content-bg;
  border-radius: @radius-base;
  padding: @spacing-lg;
  max-height: 400px;
  overflow-y: auto;
}
.doc-content {
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: @font-size-sm;
  line-height: 1.8;
  color: @text-primary;
  white-space: pre-wrap;
}

.chat-area { display: flex; flex-direction: column; height: 320px; }
.chat-messages { flex: 1; overflow-y: auto; padding: @spacing-lg; }
.chat-msg {
  display: flex; gap: @spacing-sm; margin-bottom: @spacing-md;
  &--user { flex-direction: row-reverse; }
}
.chat-avatar {
  width: 32px; height: 32px; border-radius: 50%;
  background: @brand-gradient; color: white;
  display: flex; align-items: center; justify-content: center;
  font-size: @font-size-xs; font-weight: @font-weight-semibold;
  flex-shrink: 0;
}
.chat-bubble {
  background: @content-bg;
  padding: @spacing-sm @spacing-md;
  border-radius: @radius-base;
  font-size: @font-size-sm;
  max-width: 70%;
  .chat-msg--user & { background: @brand-primary; color: white; }
}
.chat-input { padding: @spacing-md @spacing-lg; border-top: 1px solid @divider-color; }

.risk-summary {
  display: flex; justify-content: space-around;
  padding: @spacing-md 0;
}
.risk-stat { text-align: center; }
.risk-stat-num {
  font-size: @font-size-2xl; font-weight: @font-weight-bold;
  &--high { color: @danger; }
  &--mid { color: @warning; }
  &--low { color: @info; }
}
.risk-stat-label { font-size: @font-size-xs; color: @text-secondary; }

.risk-list { display: flex; flex-direction: column; gap: @spacing-md; }
.risk-card {
  background: @card-bg;
  border-radius: @radius-base;
  border: 1px solid @border-color;
  border-left: 3px solid;
  padding: @spacing-md;
  &--高风险 { border-left-color: @danger; }
  &--中风险 { border-left-color: @warning; }
  &--低风险 { border-left-color: @info; }
}
.risk-card-header {
  display: flex; align-items: center; justify-content: space-between;
  margin-bottom: @spacing-sm;
}
.risk-source { font-size: @font-size-xs; color: @text-tertiary; }
.risk-content { font-size: @font-size-sm; color: @text-primary; line-height: 1.5; margin-bottom: @spacing-sm; }
.risk-suggestion {
  display: flex; gap: @spacing-xs; align-items: flex-start;
  font-size: @font-size-xs; color: @text-secondary;
  background: @content-bg;
  padding: @spacing-sm; border-radius: @radius-sm;
}
</style>
```

- [ ] **Step 2: 提交**

```bash
git add user-web/src/views/bid-review/
git commit -m "feat(user-web): add bid review page with 3-column layout"
```

---

## Task 13: user-web Standards 标准查询页面

**Files:**
- Create: `d:\AI\DredgeAI\user-web\src\views\standards\index.vue`

- [ ] **Step 1: 创建 Standards 页面**

```vue
<template>
  <div class="page-container">
    <PageHeader title="标准查询" description="自然语言检索行业标准与规范条款" />

    <!-- 查询栏 -->
    <SectionCard class="mb-16">
      <a-input-search
        v-model:value="queryInput"
        placeholder="输入关键词、标准编号或自然语言查询..."
        enter-button="查询"
        size="large"
        @search="handleSearch"
      />
      <div class="quick-questions">
        <span class="quick-label">推荐问题：</span>
        <a-tag
          v-for="(q, i) in recommendedQuestions"
          :key="i"
          class="quick-tag"
          @click="queryInput = q; handleSearch()"
        >
          {{ q }}
        </a-tag>
      </div>
    </SectionCard>

    <a-row :gutter="[16, 16]">
      <!-- 左栏：查询结果 -->
      <a-col :span="16">
        <SectionCard title="查询结果" class="mb-16">
          <template #extra>
            <a-tag color="blue">命中 {{ results.length }} 条</a-tag>
          </template>
          <div class="result-list">
            <div
              v-for="item in results"
              :key="item.id"
              class="result-card"
            >
              <div class="result-header">
                <span class="result-code">{{ item.code }}</span>
                <span class="result-title">{{ item.title }}</span>
              </div>
              <div class="result-match">
                <tag-outlined />
                <span>{{ item.match }}</span>
              </div>
              <div class="result-excerpt">{{ item.excerpt }}</div>
              <div class="result-source">
                <link-outlined />
                <span>{{ item.source }}</span>
              </div>
            </div>
          </div>
        </SectionCard>
      </a-col>

      <!-- 右栏：历史 + 分类 -->
      <a-col :span="8">
        <SectionCard title="查询历史" class="mb-16">
          <a-list :data-source="history" size="small">
            <template #renderItem="{ item }">
              <a-list-item class="history-item" @click="queryInput = item.query; handleSearch()">
                <a-list-item-meta>
                  <template #title>
                    <span class="history-query">{{ item.query }}</span>
                  </template>
                  <template #description>
                    {{ item.date }} · 命中 {{ item.resultCount }} 条
                  </template>
                </a-list-item-meta>
              </a-list-item>
            </template>
          </a-list>
        </SectionCard>

        <SectionCard title="标准分类">
          <a-tree :tree-data="categoryTree" :default-expand-all="true">
            <template #title="{ name, count }">
              <span class="tree-name">{{ name }}</span>
              <span class="tree-count">{{ count }}</span>
            </template>
          </a-tree>
        </SectionCard>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { TagOutlined, LinkOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import SectionCard from '@/components/SectionCard.vue'
import { getStandardResult, getStandardHistory, getStandardCategories, getRecommendedQuestions } from '@/api/modules/standard'
import type { StandardResult, StandardSearchHistory, StandardCategory } from '@/types'

const queryInput = ref('')
const results = ref<StandardResult[]>([])
const history = ref<StandardSearchHistory[]>([])
const recommendedQuestions = ref<string[]>([])
const categories = ref<StandardCategory[]>([])

const categoryTree = computed(() => categories.value.map(mapCategory))

// 转换标准分类为 a-tree 数据格式
function mapCategory(c: StandardCategory) {
  return {
    key: c.id,
    name: c.name,
    count: c.count,
    children: c.children?.map(mapCategory),
  }
}

function handleSearch(): void {
  if (!queryInput.value.trim()) return
  // mock 场景下直接展示已有结果
}

onMounted(async () => {
  const [r, h, q, c] = await Promise.all([
    getStandardResult(), getStandardHistory(), getRecommendedQuestions(), getStandardCategories(),
  ])
  results.value = r
  history.value = h
  recommendedQuestions.value = q
  categories.value = c
})
</script>

<script lang="ts">
import { computed } from 'vue'
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.mb-16 { margin-bottom: @spacing-lg; }

.quick-questions {
  display: flex; align-items: center; flex-wrap: wrap; gap: @spacing-sm;
  margin-top: @spacing-md;
}
.quick-label { font-size: @font-size-sm; color: @text-secondary; }
.quick-tag {
  cursor: pointer;
  &:hover { color: @brand-primary; border-color: @brand-primary; }
}

.result-list { display: flex; flex-direction: column; gap: @spacing-md; }
.result-card {
  background: @content-bg;
  border-radius: @radius-base;
  padding: @spacing-lg;
  border: 1px solid @border-color;
  transition: all @transition-base;
  &:hover { border-color: @brand-primary; box-shadow: @shadow-sm; }
}
.result-header {
  display: flex; align-items: center; gap: @spacing-sm; margin-bottom: @spacing-sm;
}
.result-code {
  font-size: @font-size-sm; font-weight: @font-weight-semibold;
  color: @brand-primary;
  background: fade(@brand-primary, 10%);
  padding: 2px @spacing-sm; border-radius: @radius-sm;
}
.result-title { font-size: @font-size-base; font-weight: @font-weight-medium; color: @text-primary; }
.result-match {
  display: flex; align-items: center; gap: @spacing-xs;
  font-size: @font-size-xs; color: @text-secondary;
  margin-bottom: @spacing-sm;
}
.result-excerpt {
  font-size: @font-size-sm; color: @text-primary;
  line-height: 1.6; margin-bottom: @spacing-sm;
  padding-left: @spacing-md;
  border-left: 2px solid @divider-color;
}
.result-source {
  display: flex; align-items: center; gap: @spacing-xs;
  font-size: @font-size-xs; color: @text-tertiary;
}

.history-item { cursor: pointer; &:hover .history-query { color: @brand-primary; } }
.history-query { font-size: @font-size-sm; }

.tree-name { margin-right: @spacing-sm; }
.tree-count {
  font-size: @font-size-xs; color: @text-tertiary;
  background: @divider-color; padding: 0 @spacing-xs; border-radius: @radius-sm;
}
</style>
```

> **注意**：上面 `<script setup>` 中使用了 `computed` 但导入在第二个 `<script lang="ts">` 块。实际实现时应将 `import { computed } from 'vue'` 合并到 `<script setup>` 顶部。

- [ ] **Step 2: 修正 Standards 页面的 script 块**

将 `<script setup>` 和 `<script lang="ts">` 合并为单一 `<script setup lang="ts">`，确保 `computed` 在顶部导入：

```vue
<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { TagOutlined, LinkOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import SectionCard from '@/components/SectionCard.vue'
import { getStandardResult, getStandardHistory, getStandardCategories, getRecommendedQuestions } from '@/api/modules/standard'
import type { StandardResult, StandardSearchHistory, StandardCategory } from '@/types'

const queryInput = ref('')
const results = ref<StandardResult[]>([])
const history = ref<StandardSearchHistory[]>([])
const recommendedQuestions = ref<string[]>([])
const categories = ref<StandardCategory[]>([])

const categoryTree = computed(() => categories.value.map(mapCategory))

// 转换标准分类为 a-tree 数据格式
function mapCategory(c: StandardCategory) {
  return {
    key: c.id,
    name: c.name,
    count: c.count,
    children: c.children?.map(mapCategory),
  }
}

function handleSearch(): void {
  if (!queryInput.value.trim()) return
}

onMounted(async () => {
  const [r, h, q, c] = await Promise.all([
    getStandardResult(), getStandardHistory(), getRecommendedQuestions(), getStandardCategories(),
  ])
  results.value = r
  history.value = h
  recommendedQuestions.value = q
  categories.value = c
})
</script>
```

- [ ] **Step 3: 提交**

```bash
git add user-web/src/views/standards/
git commit -m "feat(user-web): add standards query page"
```

---

## Task 14: user-web Profile 个人中心页面

**Files:**
- Create: `d:\AI\DredgeAI\user-web\src\views\profile\index.vue`

- [ ] **Step 1: 创建 Profile 页面**

```vue
<template>
  <div class="page-container">
    <PageHeader title="个人中心" description="管理个人信息与偏好设置" />

    <a-row :gutter="[24, 24]">
      <a-col :span="8">
        <!-- 用户卡 -->
        <SectionCard class="profile-card">
          <div class="profile-header">
            <a-avatar :size="72" :style="{ background: '@{brand-gradient}' }">
              {{ userStore.userInfo?.name?.[0] || 'U' }}
            </a-avatar>
            <div class="profile-name">{{ userStore.userInfo?.name || '用户' }}</div>
            <div class="profile-role">{{ userStore.userInfo?.position }}</div>
          </div>
          <a-descriptions :column="1" size="small" class="profile-desc">
            <a-descriptions-item label="部门">{{ userStore.userInfo?.department }}</a-descriptions-item>
            <a-descriptions-item label="邮箱">{{ userStore.userInfo?.email }}</a-descriptions-item>
            <a-descriptions-item label="电话">{{ userStore.userInfo?.phone }}</a-descriptions-item>
          </a-descriptions>
          <div class="scope-section">
            <div class="scope-title">授权范围</div>
            <div class="scope-tags">
              <a-tag v-for="scope in (userStore.userInfo?.authorizedScopes || [])" :key="scope" color="cyan">
                {{ scope }}
              </a-tag>
            </div>
          </div>
        </SectionCard>
      </a-col>

      <a-col :span="16">
        <!-- 偏好设置 -->
        <SectionCard title="偏好设置" class="mb-16">
          <a-form layout="vertical">
            <a-form-item label="界面主题">
              <a-radio-group v-model:value="preferences.theme">
                <a-radio-button value="light">浅色</a-radio-button>
                <a-radio-button value="dark">深色</a-radio-button>
                <a-radio-button value="auto">跟随系统</a-radio-button>
              </a-radio-group>
            </a-form-item>
            <a-form-item label="语言">
              <a-radio-group v-model:value="preferences.language">
                <a-radio-button value="zh-CN">简体中文</a-radio-button>
                <a-radio-button value="en-US">English</a-radio-button>
              </a-radio-group>
            </a-form-item>
            <a-form-item label="通知偏好">
              <a-checkbox-group v-model:value="preferences.notifications" :options="notifOptions" />
            </a-form-item>
            <a-form-item>
              <a-button type="primary">保存设置</a-button>
            </a-form-item>
          </a-form>
        </SectionCard>

        <!-- 最近活动 -->
        <SectionCard title="最近活动">
          <a-timeline>
            <a-timeline-item v-for="(act, i) in recentActivities" :key="i" :color="act.color">
              <div class="activity-title">{{ act.title }}</div>
              <div class="activity-time">{{ act.time }}</div>
            </a-timeline-item>
          </a-timeline>
        </SectionCard>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import PageHeader from '@/components/PageHeader.vue'
import SectionCard from '@/components/SectionCard.vue'
import { useUserStore } from '@/stores/user'

const userStore = useUserStore()

const preferences = ref({
  theme: 'light',
  language: 'zh-CN',
  notifications: ['business', 'system'],
})

const notifOptions = [
  { label: '业务通知', value: 'business' },
  { label: '系统通知', value: 'system' },
  { label: '审计日志', value: 'audit' },
]

const recentActivities = [
  { title: '完成 AI 审标任务：XX 项目招标文件风险分析', time: '2026-07-17 14:35', color: 'green' },
  { title: '查询标准：GB/T 19001 质量管理体系', time: '2026-07-17 10:15', color: 'blue' },
  { title: '上传文件：XX_项目_招标文件.pdf', time: '2026-07-17 14:30', color: 'gray' },
  { title: '生成合同审查报告：供应商协议 v3', time: '2026-07-15 11:25', color: 'green' },
  { title: '登录系统', time: '2026-07-15 08:50', color: 'gray' },
]
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.mb-16 { margin-bottom: @spacing-lg; }

.profile-card { text-align: center; }
.profile-header {
  padding: @spacing-lg 0;
  display: flex; flex-direction: column; align-items: center; gap: @spacing-sm;
}
.profile-name {
  font-size: @font-size-xl; font-weight: @font-weight-semibold; color: @text-primary;
}
.profile-role { font-size: @font-size-sm; color: @text-secondary; }
.profile-desc { text-align: left; margin: @spacing-lg 0; }
.scope-section { border-top: 1px solid @divider-color; padding-top: @spacing-lg; text-align: left; }
.scope-title { font-size: @font-size-sm; color: @text-secondary; margin-bottom: @spacing-sm; }
.scope-tags { display: flex; flex-wrap: wrap; gap: @spacing-xs; }

.activity-title { font-size: @font-size-sm; color: @text-primary; }
.activity-time { font-size: @font-size-xs; color: @text-tertiary; margin-top: 2px; }
</style>
```

- [ ] **Step 2: 提交**

```bash
git add user-web/src/views/profile/
git commit -m "feat(user-web): add profile page"
```

---

## Task 15: user-web API 管理页面

**Files:**
- Create: `d:\AI\DredgeAI\user-web\src\views\api\index.vue`

- [ ] **Step 1: 创建 API 管理页面**

```vue
<template>
  <div class="page-container">
    <PageHeader title="API 管理" description="管理 API Key、配额与调用统计">
      <template #extra>
        <a-button type="primary" @click="showCreateModal = true">
          <plus-outlined />
          创建 Key
        </a-button>
      </template>
    </PageHeader>

    <a-row :gutter="[24, 24]">
      <a-col :span="16">
        <!-- Key 列表 -->
        <SectionCard title="API Key 列表" nopad>
          <a-table
            :data-source="apiKeys"
            :columns="columns"
            :pagination="false"
            row-key="id"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'key'">
                <span class="key-text">{{ record.key }}</span>
                <a-button type="link" size="small" @click="copyKey(record.fullKey)">
                  <copy-outlined />
                </a-button>
              </template>
              <template v-else-if="column.key === 'status'">
                <StatusTag :status="record.status" />
              </template>
              <template v-else-if="column.key === 'usage'">
                <a-progress :percent="Math.round(record.usage / record.quota * 100)" :size="'small'" />
                <div class="usage-text">{{ record.usage }} / {{ record.quota }}</div>
              </template>
              <template v-else-if="column.key === 'action'">
                <a-button type="link" size="small">编辑</a-button>
                <a-button type="link" size="small" danger>禁用</a-button>
              </template>
            </template>
          </a-table>
        </SectionCard>
      </a-col>

      <a-col :span="8">
        <!-- 按模型用量 -->
        <SectionCard title="按模型用量" class="mb-16">
          <ChartContainer :option="modelPieOption" height="240px" />
        </SectionCard>

        <!-- 按 Key 用量 -->
        <SectionCard title="按 Key 用量">
          <ChartContainer :option="keyBarOption" height="200px" />
        </SectionCard>
      </a-col>
    </a-row>

    <!-- 创建 Key 弹窗 -->
    <a-modal v-model:open="showCreateModal" title="创建 API Key" @ok="handleCreate">
      <a-form layout="vertical">
        <a-form-item label="Key 名称" required>
          <a-input v-model:value="newKey.name" placeholder="如：生产环境" />
        </a-form-item>
        <a-form-item label="模型类型" required>
          <a-select v-model:value="newKey.modelType" :options="modelOptions" placeholder="选择模型" />
        </a-form-item>
        <a-form-item label="配额">
          <a-input-number v-model:value="newKey.quota" :min="1000" :step="1000" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, CopyOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import SectionCard from '@/components/SectionCard.vue'
import StatusTag from '@/components/StatusTag.vue'
import ChartContainer from '@/components/ChartContainer.vue'
import { getApiKeyList, getModelTypes, getUsageByModel, getUsageByKey } from '@/api/modules/apikey'
import type { ApiKey, ModelType, UsageByModel, UsageByKey } from '@/types'

const apiKeys = ref<ApiKey[]>([])
const modelTypes = ref<ModelType[]>([])
const usageByModel = ref<UsageByModel[]>([])
const usageByKey = ref<UsageByKey[]>([])
const showCreateModal = ref(false)

const newKey = ref({ name: '', modelType: '', quota: 10000 })

const columns = [
  { title: '名称', dataIndex: 'name', key: 'name' },
  { title: 'Key', key: 'key' },
  { title: '模型', dataIndex: 'modelType', key: 'modelType' },
  { title: '状态', key: 'status' },
  { title: '用量', key: 'usage' },
  { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt' },
  { title: '操作', key: 'action' },
]

const modelOptions = computed(() => modelTypes.value.map((m) => ({ label: m.name, value: m.name })))

// 模型用量饼图
const modelPieOption = computed(() => ({
  tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
  legend: { bottom: 0, type: 'scroll' },
  series: [{
    type: 'pie',
    radius: ['40%', '70%'],
    avoidLabelOverlap: false,
    itemStyle: { borderRadius: 8, borderColor: '#fff', borderWidth: 2 },
    label: { show: false },
    emphasis: { label: { show: true, fontSize: 14, fontWeight: 'bold' } },
    data: usageByModel.value.map((u) => ({ name: u.modelName, value: u.calls })),
    color: ['#0EA5E9', '#06B6D4', '#10B981', '#F59E0B', '#EF4444'],
  }],
}))

// Key 用量柱状图
const keyBarOption = computed(() => ({
  tooltip: { trigger: 'axis' },
  grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
  xAxis: { type: 'category', data: usageByKey.value.map((u) => u.keyName) },
  yAxis: { type: 'value' },
  series: [{
    type: 'bar',
    data: usageByKey.value.map((u) => u.calls),
    itemStyle: { color: '#0EA5E9', borderRadius: [4, 4, 0, 0] },
    barWidth: '40%',
  }],
}))

function copyKey(key: string): void {
  navigator.clipboard.writeText(key)
  message.success('已复制到剪贴板')
}

function handleCreate(): void {
  if (!newKey.value.name || !newKey.value.modelType) {
    message.warning('请填写完整信息')
    return
  }
  message.success('API Key 创建成功')
  showCreateModal.value = false
  newKey.value = { name: '', modelType: '', quota: 10000 }
}

onMounted(async () => {
  const [k, m, um, ub] = await Promise.all([
    getApiKeyList(), getModelTypes(), getUsageByModel(), getUsageByKey(),
  ])
  apiKeys.value = k
  modelTypes.value = m
  usageByModel.value = um
  usageByKey.value = ub
})
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.mb-16 { margin-bottom: @spacing-lg; }

.key-text {
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: @font-size-xs;
  color: @text-primary;
  background: @content-bg;
  padding: 2px @spacing-sm;
  border-radius: @radius-sm;
}
.usage-text {
  font-size: 10px;
  color: @text-tertiary;
  margin-top: 2px;
}
</style>
```

- [ ] **Step 2: 提交**

```bash
git add user-web/src/views/api/
git commit -m "feat(user-web): add API management page"
```

---

## Task 16: user-web 验证与启动测试

- [ ] **Step 1: 类型检查**

Run: `pnpm --filter user-web typecheck`
Expected: 无 TypeScript 错误

- [ ] **Step 2: 构建测试**

Run: `pnpm --filter user-web build`
Expected: 构建成功，产出 `user-web/dist/`

- [ ] **Step 3: 启动 dev server 验证**

Run: `pnpm --filter user-web dev`
Expected: 服务器在 `http://localhost:5373` 启动，所有 6 个页面可访问

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "test(user-web): verify typecheck and build pass"
```

---
