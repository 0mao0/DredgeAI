# Shared 分层与多端架构重构实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 monorepo 中"源码 alias 式"的 shared 重构为"core/web 分层 + manifest 驱动 + 协议契约统一"的多端架构，使 user-web/admin-web 共享同一套框架无关内核，并为未来 app 端接入预留零成本路径。

**Architecture:** 沿用 pnpm workspace + Vite alias 方式（不引入构建产物），但把 `packages/shared/src` 内部按职责拆为 `core/`（框架无关：types/协议/request 工厂/URL 契约/纯函数）与 `web/`（Vue 专属：组件/composables/stores）。引入"应用清单（AppManifest）"机制让 admin 发布状态实时驱动 user-web 可见路由。统一双端主题 store、补齐权限守卫与错误边界、预留 i18n 骨架。

**Tech Stack:** Vue 3 + TypeScript + Vite + Pinia + Vue Router 4 + Ant Design Vue 4 + Less + axios + axios-mock-adapter + pnpm workspace

---

## 决策摘要

| 决策项 | 选择 |
|---|---|
| 范围 | 全部 6 个 Phase |
| app 端 | 暂不接入，但 core 严格保持框架无关 |
| 包构建 | 保持源码 alias，但内部按 core/web 分目录 |
| 提交 | 每个 Task 一次提交 |

## 当前痛点（计划依据）

1. shared 是源码 alias，未分层，无框架无关内核
2. 路由全硬编码，admin 发布无法驱动 user 可见性
3. 视图层硬编码 mock，绕过 API 层（[user-web/src/views/api/index.vue](file:///d:/AI/DredgeAI/user-web/src/views/api/index.vue)、[admin-web/src/views/api/index.vue](file:///d:/AI/DredgeAI/admin-web/src/views/api/index.vue)）
4. 双端 request.ts 100% 重复，ABP 类型重复定义
5. 主题系统分裂：user-web 用 `useThemeStore`，admin-web 用 `useTheme` composable，且 `ThemeToggle` 共享组件只绑后者
6. Store 命名冲突：两个 `useAppStore` 职责完全不同
7. types 单文件 287 行巨石
8. 双端 API 路径不统一（user `/key/list`，admin `/apikey/list`）
9. 缺路由守卫、权限指令、ErrorBoundary
10. mock/index.ts 仍有 console.log 残留

## 目标文件结构

```
packages/shared/src/
├── core/                       # 框架无关内核（未来 app 端可零成本复用）
│   ├── types/
│   │   ├── common.ts           # Pagination、Chart 等
│   │   ├── abp.ts              # AbpErrorInfo、AbpErrorResponse、PagedResult
│   │   ├── apikey.ts
│   │   ├── application.ts      # ApplicationItem、SubApp、AppManifest
│   │   ├── dashboard.ts
│   │   ├── user.ts             # AdminUserInfo、UserUserInfo
│   │   ├── bid.ts
│   │   ├── standard.ts
│   │   ├── task.ts
│   │   ├── file.ts
│   │   ├── permission.ts
│   │   ├── datasource.ts
│   │   ├── analytics.ts
│   │   └── index.ts            # barrel
│   ├── http/
│   │   ├── createRequest.ts    # axios 工厂，参数化 baseURL/token key
│   │   ├── abp.ts              # ABP 响应拦截器
│   │   └── types.ts            # CreateRequestOptions
│   ├── api/
│   │   └── urls.ts             # URL 契约：资源 key → path（不含前缀）
│   ├── utils/
│   │   └── format.ts
│   └── index.ts
├── web/                        # Vue 专属
│   ├── components/             # ChartContainer / DataSkeleton / Logo / MetricCard / PageHeader / SectionCard / ThemeToggle
│   ├── composables/
│   │   ├── useCssVar.ts
│   │   └── useThemeStore.ts    # 统一主题 store（参数化端标识）
│   ├── stores/
│   │   └── useSidebarStore.ts  # 通用侧边栏折叠状态
│   ├── directives/
│   │   └── permission.ts       # v-permission
│   ├── components/
│   │   └── ErrorBoundary.vue
│   ├── styles/                 # global / reset / themes / variables
│   └── index.ts
├── mock/                       # 共享 mock 数据（保持现状）
│   └── data/
└── index.ts                    # 顶层 barrel：re-export core + web
```

user-web / admin-web 内部保持现有结构，但：
- `src/api/request.ts` 改为调用 `createRequest(...)`
- `src/stores/theme.ts` 删除，改用 `@shared/web/composables/useThemeStore`
- `src/stores/app.ts` 拆分：sidebar 部分用 `useSidebarStore`，业务部分留下
- `src/router/index.ts` 改为 manifest 驱动

---

## Phase 1: shared/core 分层（框架无关内核）

### Task 1.1: 建立 core 目录骨架与 barrel

**Files:**
- Create: `packages/shared/src/core/index.ts`
- Create: `packages/shared/src/core/types/index.ts`（空 barrel，后续 Task 填充）
- Create: `packages/shared/src/core/http/index.ts`
- Create: `packages/shared/src/core/api/index.ts`
- Create: `packages/shared/src/core/utils/index.ts`

- [ ] **Step 1: 创建目录与空 barrel 文件**

`packages/shared/src/core/types/index.ts`：
```ts
// 类型 barrel，按领域 sub-bundle 导出（后续 Task 填充）
export * from './common'
export * from './abp'
export * from './apikey'
export * from './application'
export * from './dashboard'
export * from './user'
export * from './bid'
export * from './standard'
export * from './task'
export * from './file'
export * from './permission'
export * from './datasource'
export * from './analytics'
```

`packages/shared/src/core/index.ts`：
```ts
// shared/core：框架无关内核，可被任意前端框架复用
export * from './types'
export * from './http'
export * from './api'
export * from './utils'
```

`packages/shared/src/core/http/index.ts`、`core/api/index.ts`、`core/utils/index.ts`：先创建空文件，仅 `export {}` 占位，后续 Task 填充。

- [ ] **Step 2: 验证目录结构**

运行：`pnpm --filter @dredge/shared typecheck`（若 shared 包没有 typecheck 脚本，跳过；当前 shared 包未配置独立 typecheck，依赖双端 typecheck 覆盖）

- [ ] **Step 3: 提交**

```bash
git add packages/shared/src/core
git commit -m "refactor(shared): scaffold core/ layer skeleton"
```

---

### Task 1.2: 拆分 types 到 core/types 按领域

**Files:**
- Create: `packages/shared/src/core/types/common.ts`
- Create: `packages/shared/src/core/types/abp.ts`
- Create: `packages/shared/src/core/types/apikey.ts`
- Create: `packages/shared/src/core/types/application.ts`
- Create: `packages/shared/src/core/types/dashboard.ts`
- Create: `packages/shared/src/core/types/user.ts`
- Create: `packages/shared/src/core/types/bid.ts`
- Create: `packages/shared/src/core/types/standard.ts`
- Create: `packages/shared/src/core/types/task.ts`
- Create: `packages/shared/src/core/types/file.ts`
- Create: `packages/shared/src/core/types/permission.ts`
- Create: `packages/shared/src/core/types/datasource.ts`
- Create: `packages/shared/src/core/types/analytics.ts`
- Modify: `packages/shared/src/types/index.ts`（改为 re-export core/types）
- Modify: `packages/shared/src/core/types/application.ts` 增加 `AppManifest` 类型

- [ ] **Step 1: 创建 common.ts（通用图表与分页）**

`packages/shared/src/core/types/common.ts`：
```ts
/** 分页参数 */
export interface Pagination {
  page: number
  pageSize: number
  total: number
}

// ---------- 通用图表类型 ----------

export interface ChartSeries {
  name: string
  data: number[]
}

export interface LineChartData {
  categories: string[]
  series: ChartSeries[]
}

export interface PieChartData {
  name: string
  data: { name: string; value: number }[]
}
```

- [ ] **Step 2: 创建 abp.ts（ABP 协议类型，从 request.ts 抽出）**

`packages/shared/src/core/types/abp.ts`：
```ts
/** ABP 错误响应结构 */
export interface AbpErrorInfo {
  code: string | null
  message: string | null
  details: string | null
  data: Record<string, unknown> | null
  validationErrors: Array<{ message: string | null; members: string[] | null }> | null
}

export interface AbpErrorResponse {
  error: AbpErrorInfo
}

/** 分页查询响应 */
export interface PagedResult<T> {
  items: T[]
  totalCount: number
}
```

- [ ] **Step 3: 创建 apikey.ts**

`packages/shared/src/core/types/apikey.ts`：
```ts
export interface ApiUsageStats {
  totalTokens: number
  totalCalls: number
}

export interface UsageByModel {
  modelName: string
  calls: number
  share: number
}

export interface UsageByKey {
  keyName: string
  calls: number
  share: number
}

export interface UsageTimeSeries {
  categories: string[]
  byModel: { modelName: string; data: number[] }[]
  byKey: { keyName: string; data: number[] }[]
  byName: { name: string; data: number[] }[]
}

export interface ModelType {
  id: string
  name: string
  provider: string
  description?: string
}

/** API Key（统一结构，admin 侧字段更全） */
export interface ApiKey {
  id: string
  name: string
  key: string
  fullKey: string
  modelType: string
  /** 该 Key 归属的应用（admin 侧用于管理"哪些人可以用哪些 API"） */
  app?: string
  status: '启用' | '禁用'
  createdAt: string
  expiredAt?: string
  lastUsed?: string
  quota: number
  usage: number
  docUrl: string
}
```

- [ ] **Step 4: 创建 application.ts（含新增的 AppManifest）**

`packages/shared/src/core/types/application.ts`：
```ts
/** 子应用：admin 模块发布后面向 user-web 的可订阅单元 */
export interface SubApp {
  id: string
  name: string
  category: '通用' | '经营' | '设计' | '施工'
  parentAppId: string
  parentAppName: string
  route: string
  icon: string
  version: string
  status: '已发布' | '已下架'
  description?: string
  scope?: '所有' | '部分'
}

/** admin 侧应用目录 */
export interface ApplicationItem {
  id: string
  name: string
  category: '通用' | '经营' | '设计' | '施工'
  manager: string
  version: string
  status: '运营中' | '已下架' | '开发中'
  userCount: number
  apiCalls: number
  createdAt: string
  icon: string
  subApps?: SubApp[]
  scope?: '所有' | '部分'
}

/** user-web 侧应用卡片（由 ApplicationItem/SubApp 推导） */
export interface AppCard {
  id: string
  title: string
  description: string
  category: '通用' | '设计' | '施工' | '经营'
  icon: string
  status: '已授权' | '待申请' | '已下架'
  route?: string
  version?: string
  pinned?: boolean
}

/**
 * 应用清单：每个 user-web 应用对应一份 manifest，描述路由/组件/权限等元数据。
 * 路由表不再硬编码，而是由 manifest 数组动态生成。
 */
export interface AppManifest {
  /** 唯一 id，与 ApplicationItem.id 或 SubApp.id 对应 */
  id: string
  /** 路由路径（必须以 / 开头） */
  route: string
  /** 路由 name */
  name: string
  /** 菜单标题 */
  title: string
  /** antd 图标名（与 SubApp.icon 同一命名空间） */
  icon: string
  /** 视图组件的动态 import 函数 */
  component: () => Promise<unknown>
  /** 默认是否在侧边栏可见（用户可在 profile 页勾选） */
  defaultVisible?: boolean
  /** 所需权限码（可选，路由守卫消费） */
  requiredPermission?: string
  /** 分类标签（用于侧边栏分组） */
  category?: '通用' | '经营' | '设计' | '施工'
}
```

- [ ] **Step 5: 创建 dashboard.ts（admin 专用）**

`packages/shared/src/core/types/dashboard.ts`：
```ts
export interface AdminStats {
  totalUsers: number
  totalApps: number
  totalApiCalls: number
  activeUsers: number
  userTrend: number
  appTrend: number
  apiTrend: number
  activeUserTrend: number
}

export interface DashboardMetric {
  id: string
  title: string
  value: string | number
  suffix?: string
  trend?: number
  trendUp?: boolean
  icon?: string
  color?: string
}

export interface SystemLog {
  id: string
  type: '操作日志' | '登录日志' | '系统错误' | '安全告警'
  operator: string
  content: string
  ip?: string
  createdAt: string
  level?: 'info' | 'warning' | 'error'
}

export interface DataSource {
  id: string
  name: string
  type: 'mysql' | 'postgresql' | 'api'
  status: '已连接' | '连接失败' | '未配置'
  lastSync?: string
  description?: string
}
```

- [ ] **Step 6: 创建 user.ts**

`packages/shared/src/core/types/user.ts`：
```ts
export interface AdminUserInfo {
  id: string
  username: string
  name: string
  email: string
  phone: string
  role: 'super_admin' | 'admin' | 'operator'
  department: string
  avatar?: string
  status: '启用' | '禁用'
  createdAt: string
  lastLogin?: string
}

export interface UserUserInfo {
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
```

- [ ] **Step 7: 创建剩余领域类型文件**

`packages/shared/src/core/types/bid.ts`：
```ts
export interface BidReviewStep {
  title: string
  description: string
  status: 'wait' | 'process' | 'finish' | 'error'
}

export interface RiskItem {
  id: string
  level: '高风险' | '中风险' | '低风险'
  content: string
  source: string
  suggestion?: string
}

export interface BidReviewSession {
  id: string
  document: string
  date: string
  riskCount: number
  status: '已完成' | '进行中'
  snippets?: { role: 'user' | 'assistant'; content: string }[]
}
```

`packages/shared/src/core/types/standard.ts`：
```ts
export interface StandardResult {
  id: string
  code: string
  title: string
  match: string
  excerpt: string
  source?: string
}

export interface StandardSearchHistory {
  id: string
  query: string
  date: string
  resultCount: number
}

export interface StandardCategory {
  id: string
  name: string
  count: number
  children?: StandardCategory[]
}
```

`packages/shared/src/core/types/task.ts`：
```ts
export interface TaskItem {
  id: string
  title: string
  status: '进行中' | '已完成' | '已暂停' | '已失败'
  updatedAt: string
  app?: string
  progress?: number
}

export interface QuickTask {
  id: string
  title: string
  tag: string
  route: string
  icon: string
}
```

`packages/shared/src/core/types/file.ts`：
```ts
export interface FileItem {
  id: string
  name: string
  type: 'pdf' | 'docx' | 'xlsx' | 'pptx' | 'image' | 'other'
  size: string
  updatedAt: string
  url?: string
}
```

`packages/shared/src/core/types/permission.ts`：
```ts
export interface PermissionItem {
  id: string
  name: string
  code: string
  type: 'menu' | 'button' | 'api' | 'data'
  parentId?: string
  description?: string
  status: '启用' | '禁用'
  sort: number
}

/** 用户级应用指标卡片（user-web dashboard 用） */
export interface MetricCard {
  id: string
  title: string
  value: string | number
  trend?: string
  trendUp?: boolean
  sparkline?: number[]
}
```

`packages/shared/src/core/types/datasource.ts`、`analytics.ts`：
```ts
// datasource.ts —— DataSource 已在 dashboard.ts 定义，此处仅 re-export 保持 barrel 完整
export type { DataSource } from './dashboard'

// analytics.ts：admin-web 分析接口的返回类型目前为 LineChartData/PieChartData，无需额外类型
// 文件保留为占位，未来扩展时填充
export {}
```

- [ ] **Step 8: 改写原 types/index.ts 为 re-export**

`packages/shared/src/types/index.ts`（覆盖原 287 行）：
```ts
// 兼容旧导入路径 @shared/types，统一 re-export 新的 core/types
export * from '../core/types'
```

- [ ] **Step 9: 验证双端类型检查通过**

运行：`pnpm typecheck`
预期：通过。所有 `import type { ... } from '@shared/types'` 或 `from '@/types'` 都应仍然有效（因为 @/types re-export 自 @shared/types，而 @shared/types re-export 自 core/types）。

- [ ] **Step 10: 提交**

```bash
git add packages/shared/src/core/types packages/shared/src/types
git commit -m "refactor(shared): split monolithic types into domain files under core/types"
```

---

### Task 1.3: 抽取 createRequest 工厂到 core/http

**Files:**
- Create: `packages/shared/src/core/http/types.ts`
- Create: `packages/shared/src/core/http/createRequest.ts`
- Create: `packages/shared/src/core/http/abp.ts`
- Modify: `packages/shared/src/core/http/index.ts`
- Modify: `user-web/src/api/request.ts`（改为调用工厂）
- Modify: `admin-web/src/api/request.ts`（改为调用工厂）

- [ ] **Step 1: 创建工厂类型**

`packages/shared/src/core/http/types.ts`：
```ts
import type { AxiosInstance } from 'axios'

/** 创建 request 实例的参数 */
export interface CreateRequestOptions {
  /** axios baseURL，例如 '/api' 或 '/api/admin' */
  baseURL: string
  /** localStorage 中存储 token 的 key */
  tokenKey: string
  /** 请求超时毫秒，默认 15000 */
  timeout?: number
  /** 是否启用 nprogress 进度条，默认 true */
  progress?: boolean
  /** 401 未授权时的回调（可选，由各端注入跳转逻辑） */
  onUnauthorized?: () => void
}

/** 工厂返回的 axios 实例（已注入 ABP 拦截器与泛型方法签名） */
export type RequestInstance = AxiosInstance
```

- [ ] **Step 2: 创建 ABP 拦截器辅助**

`packages/shared/src/core/http/abp.ts`：
```ts
import type { AxiosInstance, AxiosResponse } from 'axios'
import type { AbpErrorInfo } from '../types/abp'

/** 给 axios 实例挂载 ABP 协议响应/错误拦截器 */
export function applyAbpInterceptors(
  instance: AxiosInstance,
  opts: { onUnauthorized?: () => void; progress?: boolean } = {},
): void {
  const { onUnauthorized, progress = true } = opts

  instance.interceptors.request.use((config) => {
    if (progress) {
      // nprogress 是可选依赖，由调用方在 web 端注入；core 层不直接 import
      // 这里通过 config 上的标记让 web 端的包装器处理
      ;(config as unknown as { _startProgress?: boolean })._startProgress = true
    }
    return config
  })

  instance.interceptors.response.use(
    (response: AxiosResponse) => {
      // ABP 格式：成功响应直接返回数据体
      return response.data
    },
    (error) => {
      const status = error.response?.status
      if (status === 401 && onUnauthorized) onUnauthorized()

      const abpError: AbpErrorInfo | undefined = error.response?.data?.error
      if (abpError) {
        return Promise.reject(new Error(abpError.message || '请求失败'))
      }
      return Promise.reject(error)
    },
  )

  // 扩展 axios 实例的泛型签名
  declare module 'axios' {
    export interface AxiosInstance {
      get<T = unknown>(url: string, config?: import('axios').AxiosRequestConfig): Promise<T>
      post<T = unknown>(url: string, data?: unknown, config?: import('axios').AxiosRequestConfig): Promise<T>
      put<T = unknown>(url: string, data?: unknown, config?: import('axios').AxiosRequestConfig): Promise<T>
      patch<T = unknown>(url: string, data?: unknown, config?: import('axios').AxiosRequestConfig): Promise<T>
      delete<T = unknown>(url: string, config?: import('axios').AxiosRequestConfig): Promise<T>
    }
  }
}
```

- [ ] **Step 3: 创建 createRequest 工厂**

`packages/shared/src/core/http/createRequest.ts`：
```ts
import axios from 'axios'
import type { CreateRequestOptions, RequestInstance } from './types'
import { applyAbpInterceptors } from './abp'

/**
 * 创建带 ABP 协议拦截器的 axios 实例。
 * 框架无关：不依赖 Vue / nprogress，进度条由 web 端包装器处理。
 */
export function createRequest(opts: CreateRequestOptions): RequestInstance {
  const { baseURL, tokenKey, timeout = 15000, onUnauthorized } = opts

  const instance = axios.create({ baseURL, timeout })

  // token 注入
  instance.interceptors.request.use((config) => {
    const token = typeof localStorage !== 'undefined' ? localStorage.getItem(tokenKey) : null
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  })

  applyAbpInterceptors(instance, { onUnauthorized })

  return instance as RequestInstance
}
```

- [ ] **Step 4: 更新 http/index.ts barrel**

`packages/shared/src/core/http/index.ts`：
```ts
export * from './types'
export * from './abp'
export * from './createRequest'
```

- [ ] **Step 5: 改写 user-web/src/api/request.ts**

`user-web/src/api/request.ts`（完整覆盖）：
```ts
import nprogress from 'nprogress'
import 'nprogress/nprogress.css'
import { createRequest } from '@shared/core/http'
import { API_BASE_URL, STORAGE_TOKEN_KEY } from '@/utils/constants'

/** user-web 专属 request 实例：注入 nprogress 与未授权跳转 */
const instance = createRequest({
  baseURL: API_BASE_URL,
  tokenKey: STORAGE_TOKEN_KEY,
  onUnauthorized: () => {
    // 路由守卫层处理跳转，这里仅清理 token
    localStorage.removeItem(STORAGE_TOKEN_KEY)
  },
})

// 注入 nprogress 进度条（web 端专属，core 不感知）
instance.interceptors.request.use((config) => {
  if ((config as unknown as { _startProgress?: boolean })._startProgress !== false) {
    nprogress.start()
  }
  return config
})
instance.interceptors.response.use(
  (resp) => { nprogress.done(); return resp },
  (err) => { nprogress.done(); return Promise.reject(err) },
)

export default instance

// 兼容旧导出
export type { AbpErrorInfo, AbpErrorResponse, PagedResult } from '@shared/core/types'
export function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}
export function randomDelay(): Promise<void> {
  return delay(200 + Math.floor(Math.random() * 200))
}
```

- [ ] **Step 6: 改写 admin-web/src/api/request.ts（同上模式）**

`admin-web/src/api/request.ts`（完整覆盖）：
```ts
import nprogress from 'nprogress'
import 'nprogress/nprogress.css'
import { createRequest } from '@shared/core/http'
import { API_BASE_URL, STORAGE_TOKEN_KEY } from '@/utils/constants'

/** admin-web 专属 request 实例 */
const instance = createRequest({
  baseURL: API_BASE_URL,
  tokenKey: STORAGE_TOKEN_KEY,
  onUnauthorized: () => {
    localStorage.removeItem(STORAGE_TOKEN_KEY)
  },
})

instance.interceptors.request.use((config) => {
  nprogress.start()
  return config
})
instance.interceptors.response.use(
  (resp) => { nprogress.done(); return resp },
  (err) => { nprogress.done(); return Promise.reject(err) },
)

export default instance

export type { AbpErrorInfo, AbpErrorResponse, PagedResult } from '@shared/core/types'
export function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}
export function randomDelay(): Promise<void> {
  return delay(200 + Math.floor(Math.random() * 200))
}
```

- [ ] **Step 7: 验证双端 typecheck 通过**

运行：`pnpm typecheck`
预期：通过。

- [ ] **Step 8: 启动双端 dev 验证 mock 仍生效**

运行：`pnpm dev`
预期：访问 http://localhost:5373 与 http://localhost:5374，dashboard 等页面正常加载，无控制台报错。

- [ ] **Step 9: 提交**

```bash
git add packages/shared/src/core/http user-web/src/api/request.ts admin-web/src/api/request.ts
git commit -m "refactor(shared): extract createRequest factory to core/http, dedupe request.ts"
```

---

### Task 1.4: 抽取 URL 契约到 core/api/urls

**Files:**
- Create: `packages/shared/src/core/api/urls.ts`
- Modify: `packages/shared/src/core/api/index.ts`
- Modify: `user-web/src/api/modules/apikey.ts`（路径改用契约）
- Modify: `admin-web/src/api/modules/apikey.ts`（路径改用契约，统一命名）

- [ ] **Step 1: 创建 URL 契约**

`packages/shared/src/core/api/urls.ts`：
```ts
/**
 * URL 契约：所有 API 资源路径在此声明，双端共用同一 key。
 * 路径不含 baseURL 前缀（前缀由各端的 createRequest 注入）。
 * 修复历史问题：原 user-web 用 /key、admin-web 用 /apikey，命名不统一。
 */
export const urls = {
  // user-web
  userCurrent: '/user/current',
  appList: '/app/list',
  taskRecent: '/task/recent',
  taskQuick: '/task/quick',
  fileRecent: '/file/recent',
  bidSteps: '/bid/steps',
  bidRisks: '/bid/risks',
  bidSessions: '/bid/sessions',
  bidDocument: '/bid/document',
  standardResult: '/standard/result',
  standardHistory: '/standard/history',
  standardCategories: '/standard/categories',
  standardRecommended: '/standard/recommended',
  chartEfficiencyTrend: '/chart/efficiency-trend',

  // 共享（双端均使用 /apikey 命名，统一规范）
  apiKeyList: '/apikey/list',
  apiKeyModels: '/apikey/models',
  apiKeyUsageByModel: '/apikey/usage-by-model',
  apiKeyUsageByKey: '/apikey/usage-by-key',
  apiKeyUsageStats: '/apikey/usage-stats',
  apiKeyUsageTimeSeries: '/apikey/usage-timeseries',

  // admin-web
  adminStats: '/dashboard/stats',
  dashboardMetrics: '/dashboard/metrics',
  dashboardApiCallsTrend: '/dashboard/api-calls-trend',
  dashboardAppDistribution: '/dashboard/app-distribution',
  dashboardActiveUsersTrend: '/dashboard/active-users-trend',
  dashboardRecentLogs: '/dashboard/recent-logs',
  analyticsDailyApiCalls: '/analytics/daily-api-calls',
  analyticsModelUsage: '/analytics/model-usage',
  analyticsUserGrowth: '/analytics/user-growth',
  analyticsErrorRate: '/analytics/error-rate',
  applications: '/applications',
  permissions: '/permissions',
  datasources: '/datasources',
  adminProfile: '/profile',
} as const

export type UrlKey = keyof typeof urls
```

- [ ] **Step 2: 更新 api/index.ts barrel**

`packages/shared/src/core/api/index.ts`：
```ts
export * from './urls'
```

- [ ] **Step 3: 改写 user-web apikey 模块使用契约 + 统一路径**

`user-web/src/api/modules/apikey.ts`（覆盖）：
```ts
import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { ApiKey, ModelType, UsageByModel, UsageByKey, ApiUsageStats, UsageTimeSeries } from '@/types'

export function getApiKeyList(): Promise<ApiKey[]> {
  return request.get<ApiKey[]>(urls.apiKeyList)
}

export function getModelTypes(): Promise<ModelType[]> {
  return request.get<ModelType[]>(urls.apiKeyModels)
}

export function getUsageByModel(): Promise<UsageByModel[]> {
  return request.get<UsageByModel[]>(urls.apiKeyUsageByModel)
}

export function getUsageByKey(): Promise<UsageByKey[]> {
  return request.get<UsageByKey[]>(urls.apiKeyUsageByKey)
}

export function getUsageStats(): Promise<ApiUsageStats> {
  return request.get<ApiUsageStats>(urls.apiKeyUsageStats)
}

export function getUsageTimeSeries(range: string, extra?: Record<string, string>): Promise<UsageTimeSeries> {
  return request.get<UsageTimeSeries>(urls.apiKeyUsageTimeSeries, { params: { range, ...extra } })
}
```

- [ ] **Step 4: 改写 admin-web apikey 模块（路径改为 /apikey 而非 /apikey，但 mock 路由仍用 /api/admin/apikey/* 因 baseURL 不同）**

> **关键说明：** admin-web 的 `API_BASE_URL = '/api/admin'`，所以 `urls.apiKeyList = '/apikey/list'` 实际请求 `/api/admin/apikey/list`，与现有 mock 路由一致。无需改 mock。

`admin-web/src/api/modules/apikey.ts`（覆盖）：
```ts
import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { ApiKey, ModelType, UsageByModel, UsageByKey, ApiUsageStats, UsageTimeSeries } from '@/types'

export function getApiKeyList(): Promise<ApiKey[]> {
  return request.get<ApiKey[]>(urls.apiKeyList)
}

export function getModelTypes(): Promise<ModelType[]> {
  return request.get<ModelType[]>(urls.apiKeyModels)
}

export function getUsageByModel(): Promise<UsageByModel[]> {
  return request.get<UsageByModel[]>(urls.apiKeyUsageByModel)
}

export function getUsageByKey(): Promise<UsageByKey[]> {
  return request.get<UsageByKey[]>(urls.apiKeyUsageByKey)
}

export function getUsageStats(): Promise<ApiUsageStats> {
  return request.get<ApiUsageStats>(urls.apiKeyUsageStats)
}

export function getUsageTimeSeries(range: string, extra?: Record<string, string>): Promise<UsageTimeSeries> {
  return request.get<UsageTimeSeries>(urls.apiKeyUsageTimeSeries, { params: { range, ...extra } })
}
```

- [ ] **Step 5: 更新 user-web mock 路由（path 从 /api/key/* 改为 /api/apikey/*）**

> **背景：** user-web 的 `API_BASE_URL = '/api'`，所以 axios-mock-adapter 收到的 url 是 `/api/apikey/list`。

`user-web/src/mock/routes/apikey.ts`（修改前 6 行的 mock.onGet 路径）：
```ts
// 修改前
mock.onGet('/api/key/list').reply(wrap(() => apiKeys))
mock.onGet('/api/key/models').reply(wrap(() => modelTypes))
mock.onGet('/api/key/usage-by-model').reply(wrap(() => usageByModel))
mock.onGet('/api/key/usage-by-key').reply(wrap(() => usageByKey))
mock.onGet('/api/key/usage-stats').reply(wrap(() => ({...})))
mock.onGet('/api/key/usage-timeseries').reply(...)

// 修改后
mock.onGet('/api/apikey/list').reply(wrap(() => apiKeys))
mock.onGet('/api/apikey/models').reply(wrap(() => modelTypes))
mock.onGet('/api/apikey/usage-by-model').reply(wrap(() => usageByModel))
mock.onGet('/api/apikey/usage-by-key').reply(wrap(() => usageByKey))
mock.onGet('/api/apikey/usage-stats').reply(wrap(() => ({
  totalTokens: 28640000,
  totalCalls: 72500,
})))
mock.onGet('/api/apikey/usage-timeseries').reply((config) => {
  // 保持原逻辑不变
})
```

- [ ] **Step 6: 把其余 API 模块也迁移到 url 契约（可选但推荐，一次性做完）**

按相同模式改写以下文件，所有 `request.get('/xxx')` 中的字符串改为 `urls.xxx`：

- `user-web/src/api/modules/user.ts` → `urls.userCurrent`
- `user-web/src/api/modules/app.ts` → `urls.appList`
- `user-web/src/api/modules/task.ts` → `urls.taskRecent`、`urls.taskQuick`
- `user-web/src/api/modules/file.ts` → `urls.fileRecent`
- `user-web/src/api/modules/bid.ts` → `urls.bidSteps` 等
- `user-web/src/api/modules/standard.ts` → `urls.standardResult` 等
- `user-web/src/api/modules/chart.ts` → `urls.chartEfficiencyTrend`
- `admin-web/src/api/modules/dashboard.ts` → `urls.adminStats` 等
- `admin-web/src/api/modules/analytics.ts` → `urls.analyticsDailyApiCalls` 等
- `admin-web/src/api/modules/applications.ts` → `urls.applications`（其余扩展路径保留硬编码，因是组合接口）
- `admin-web/src/api/modules/permissions.ts` → `urls.permissions`
- `admin-web/src/api/modules/datasource.ts` → `urls.datasources`
- `admin-web/src/api/modules/profile.ts` → `urls.adminProfile`

- [ ] **Step 7: 验证 typecheck 与 dev 运行**

运行：`pnpm typecheck && pnpm dev`
预期：双端 dev 正常加载，mock 路由匹配成功（控制台无 `[mock] NO MATCH`）。

- [ ] **Step 8: 提交**

```bash
git add packages/shared/src/core/api user-web/src/api admin-web/src/api user-web/src/mock/routes/apikey.ts
git commit -m "refactor(shared): centralize API URL contracts in core/api/urls, unify apikey naming"
```

---

### Task 1.5: 迁移纯函数工具到 core/utils

**Files:**
- Create: `packages/shared/src/core/utils/format.ts`（从 `shared/src/utils/format.ts` 迁移）
- Modify: `packages/shared/src/core/utils/index.ts`
- Delete: `packages/shared/src/utils/format.ts`
- Modify: `packages/shared/src/index.ts`（更新 re-export 路径）

- [ ] **Step 1: 创建 core/utils/format.ts（内容同原文件）**

`packages/shared/src/core/utils/format.ts`：
```ts
export function formatNumber(n: number): string {
  if (n >= 1e8) return `${(n / 1e8).toFixed(2)} 亿`
  if (n >= 1e7) return `${(n / 1e7).toFixed(1)} 千万`
  if (n >= 1e4) return `${(n / 1e4).toFixed(1)} 万`
  return n.toLocaleString()
}

export function formatPercent(n: number): string {
  return `${(n * 100).toFixed(1)}%`
}
```

- [ ] **Step 2: 更新 core/utils/index.ts**

`packages/shared/src/core/utils/index.ts`：
```ts
export * from './format'
```

- [ ] **Step 3: 删除原 utils 目录**

删除：`packages/shared/src/utils/format.ts`（目录可一并删除）

- [ ] **Step 4: 更新顶层 index.ts**

`packages/shared/src/index.ts`（删除 utils 行，已由 core re-export）：
```ts
// 共享组件
export { default as ChartContainer } from './web/components/ChartContainer.vue'
export { default as DataSkeleton } from './web/components/DataSkeleton.vue'
export { default as MetricCard } from './web/components/MetricCard.vue'
export { default as PageHeader } from './web/components/PageHeader.vue'
export { default as SectionCard } from './web/components/SectionCard.vue'
export { default as ThemeToggle } from './web/components/ThemeToggle.vue'

// 共享 composables（web 端）
export { useCssVar } from './web/composables/useCssVar'
export { useThemeStore } from './web/composables/useThemeStore'

// 共享 stores
export { useSidebarStore } from './web/stores/useSidebarStore'

// 共享内核
export * from './core'

// 共享类型（向后兼容）
export * from './types'
```

> **注意：** 此 Step 提前引用了 `web/` 目录，会在 Phase 2 Task 2.1 中实际创建。本 Task 仅更新 format 引用，顶层 index.ts 的完整改写在 Task 2.1 完成。本 Task 暂时只改 format 部分：
```ts
// 仅修改 utils 那一行
export * from './core/utils'
```

- [ ] **Step 5: 验证 typecheck**

运行：`pnpm typecheck`
预期：通过。

- [ ] **Step 6: 提交**

```bash
git add packages/shared/src/core/utils packages/shared/src/utils packages/shared/src/index.ts
git commit -m "refactor(shared): move format utils to core/utils"
```

---

## Phase 2: shared/web 分层 + 主题/store 统一

### Task 2.1: 建立 web 目录骨架，迁移组件/composables

**Files:**
- Move: `packages/shared/src/components/*.vue` → `packages/shared/src/web/components/*.vue`
- Move: `packages/shared/src/composables/useCssVar.ts` → `packages/shared/src/web/composables/useCssVar.ts`
- Move: `packages/shared/src/styles/*` → `packages/shared/src/web/styles/*`
- Delete: `packages/shared/src/composables/useTheme.ts`（被新的 useThemeStore 替代，Task 2.2 完成）
- Modify: `packages/shared/src/index.ts`（完整改写）
- Modify: 各组件内 `@import '../styles/variables.less'` 路径（保持相对路径，迁移后仍正确）

- [ ] **Step 1: 创建 web 目录并移动文件**

执行（PowerShell）：
```powershell
cd d:\AI\DredgeAI\packages\shared\src
New-Item -ItemType Directory -Path web\components -Force
New-Item -ItemType Directory -Path web\composables -Force
New-Item -ItemType Directory -Path web\stores -Force
New-Item -ItemType Directory -Path web\directives -Force
New-Item -ItemType Directory -Path web\styles -Force
Move-Item components\*.vue web\components\
Move-Item composables\useCssVar.ts web\composables\
Move-Item styles\* web\styles\
Remove-Item -Recurse components, composables, styles
```

> **保留 `composables/useTheme.ts` 不删**，Task 2.2 用新的 useThemeStore 替代后再删。

- [ ] **Step 2: 检查组件内 @import 路径**

迁移后组件位置 `web/components/SectionCard.vue`，原 `@import '../styles/variables.less'` 现应改为 `@import '../styles/variables.less'`（仍是 `../`，因 web/components → web/styles 也是上一级）。

无需改动，确认即可。

- [ ] **Step 3: 完整改写 packages/shared/src/index.ts**

`packages/shared/src/index.ts`：
```ts
// ============================================================
// 顶层 barrel：同时导出 core（框架无关）与 web（Vue 专属）
// ============================================================

// core 内核
export * from './core'

// web 组件
export { default as ChartContainer } from './web/components/ChartContainer.vue'
export { default as DataSkeleton } from './web/components/DataSkeleton.vue'
export { default as Logo } from './web/components/Logo.vue'
export { default as MetricCard } from './web/components/MetricCard.vue'
export { default as PageHeader } from './web/components/PageHeader.vue'
export { default as SectionCard } from './web/components/SectionCard.vue'
export { default as ThemeToggle } from './web/components/ThemeToggle.vue'
export { default as ErrorBoundary } from './web/components/ErrorBoundary.vue'

// web composables
export { useCssVar } from './web/composables/useCssVar'
export { useThemeStore } from './web/composables/useThemeStore'

// web stores
export { useSidebarStore } from './web/stores/useSidebarStore'

// web directives
export { permissionDirective, type PermissionResolver } from './web/directives/permission'

// 兼容旧导入路径
export * from './types'
```

> **注意：** 此处提前引用了 useThemeStore、useSidebarStore、permissionDirective、ErrorBoundary，这些会在后续 Task 创建。若 typecheck 报错找不到模块，按 Task 顺序执行即可。本 Step 仅建立 barrel，后续 Task 填充实现。

- [ ] **Step 4: 创建占位文件以通过 typecheck**

临时创建以下空文件（后续 Task 填充）：

`packages/shared/src/web/composables/useThemeStore.ts`：
```ts
// 占位，Task 2.2 实现
export function useThemeStore(): unknown { return null }
```

`packages/shared/src/web/stores/useSidebarStore.ts`：
```ts
// 占位，Task 2.3 实现
export function useSidebarStore(): unknown { return null }
```

`packages/shared/src/web/directives/permission.ts`：
```ts
// 占位，Task 5.2 实现
export const permissionDirective = {}
export type PermissionResolver = () => string[]
```

`packages/shared/src/web/components/ErrorBoundary.vue`：
```vue
<!-- 占位，Task 5.3 实现 -->
<template><slot /></template>
```

- [ ] **Step 5: 验证 typecheck 通过**

运行：`pnpm typecheck`
预期：通过。

- [ ] **Step 6: 提交**

```bash
git add packages/shared/src
git commit -m "refactor(shared): reorganize into core/web layers, move components/composables/styles to web/"
```

---

### Task 2.2: 统一 useThemeStore，删除 useTheme composable

**Files:**
- Create: `packages/shared/src/web/composables/useThemeStore.ts`（替换占位）
- Delete: `packages/shared/src/composables/useTheme.ts`（如未在 Task 2.1 删除）
- Modify: `packages/shared/src/web/components/ThemeToggle.vue`（改用 store）
- Modify: `admin-web/src/App.vue`（改用 store）
- Modify: `admin-web/src/layouts/AdminLayout.vue`（已使用 ThemeToggle，无需改）
- Delete: `user-web/src/stores/theme.ts`（功能上移到 shared）
- Modify: `user-web/src/App.vue`（改用 shared 的 store）
- Modify: `user-web/src/layouts/UserLayout.vue`（改用 shared 的 store）

- [ ] **Step 1: 实现统一 useThemeStore**

`packages/shared/src/web/composables/useThemeStore.ts`（完整覆盖）：
```ts
import { defineStore } from 'pinia'
import { ref, watch, onScopeDispose } from 'vue'
import type { ThemeConfig } from 'ant-design-vue/es/config-provider/context'
import { theme as antdTheme } from 'ant-design-vue'

const { defaultAlgorithm, darkAlgorithm } = antdTheme

export type Theme = 'light' | 'dark' | 'auto'

export interface UseThemeStoreOptions {
  /** 端标识，决定 localStorage key 与默认主题 */
  scope: 'user' | 'admin'
  /** 默认主题，默认 'light' */
  defaultTheme?: Theme
}

/** 端 → storage key 映射 */
const STORAGE_KEYS: Record<UseThemeStoreOptions['scope'], string> = {
  user: 'DREDGE_AI_USER_THEME',
  admin: 'DREDGE_AI_ADMIN_THEME',
}

/** 创建带 scope 的主题 store（避免双端共享同一 store id 导致状态串扰） */
export function createThemeStore(opts: UseThemeStoreOptions) {
  const storageKey = STORAGE_KEYS[opts.scope]
  const storeName = `theme-${opts.scope}`

  return defineStore(storeName, () => {
    const theme = ref<Theme>(
      (typeof localStorage !== 'undefined' && localStorage.getItem(storageKey) as Theme) ||
      opts.defaultTheme || 'light',
    )

    const colorSchemeQuery = typeof window !== 'undefined'
      ? window.matchMedia('(prefers-color-scheme: dark)')
      : null

    function effective(): 'light' | 'dark' {
      if (theme.value === 'auto') {
        return colorSchemeQuery?.matches ? 'dark' : 'light'
      }
      return theme.value
    }

    function applyTheme(): void {
      if (typeof document === 'undefined') return
      document.documentElement.setAttribute('data-theme', effective())
    }

    function onSystemPreferenceChange(): void {
      if (theme.value === 'auto') applyTheme()
    }
    colorSchemeQuery?.addEventListener('change', onSystemPreferenceChange)
    onScopeDispose(() => colorSchemeQuery?.removeEventListener('change', onSystemPreferenceChange))

    watch(theme, (val) => {
      if (typeof localStorage !== 'undefined') localStorage.setItem(storageKey, val)
      applyTheme()
    }, { immediate: true })

    const isDark = () => effective() === 'dark'

    function toggleTheme(): void {
      theme.value = isDark() ? 'light' : 'dark'
    }

    const themeConfig = (): ThemeConfig => {
      const dark = isDark()
      return {
        algorithm: dark ? darkAlgorithm : defaultAlgorithm,
        token: {
          colorPrimary: dark ? '#60A5FA' : '#0EA5E9',
          borderRadius: 8,
          fontSize: 14,
          colorBgContainer: dark ? '#141C2C' : '#FFFFFF',
          colorBgLayout: dark ? '#0B1220' : '#F6F3EF',
          colorTextBase: dark ? '#E2E8F0' : '#1C1917',
          colorBorder: dark ? '#1E2A3E' : '#E5DFD8',
          colorBgElevated: dark ? '#1A2438' : '#FFFFFF',
        },
      }
    }

    return { theme, isDark, toggleTheme, themeConfig, applyTheme }
  }, {
    persist: { pick: ['theme'] },
  })
}

/** 双端共享的工厂函数：返回带 scope 的 useThemeStore */
export function useThemeStore(scope: UseThemeStoreOptions['scope']): ReturnType<ReturnType<typeof createThemeStore>> {
  // 简化：内部缓存 scope → store 工厂
  const factoryMap: Record<string, ReturnType<typeof createThemeStore>> = {
    user: createThemeStore({ scope: 'user' }),
    admin: createThemeStore({ scope: 'admin' }),
  }
  return factoryMap[scope]()
}
```

- [ ] **Step 2: 改写 ThemeToggle.vue 使用 store**

`packages/shared/src/web/components/ThemeToggle.vue`（完整覆盖）：
```vue
<template>
  <a-tooltip
    :title="isDark ? '切换亮色模式' : '切换暗色模式'"
    placement="bottomRight"
  >
    <a-button
      class="theme-toggle"
      shape="circle"
      size="small"
      type="text"
      @click="toggleTheme"
    >
      <template #icon>
        <BulbFilled v-if="isDark" />
        <BulbOutlined v-else />
      </template>
    </a-button>
  </a-tooltip>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { BulbFilled, BulbOutlined } from '@ant-design/icons-vue'
import { useThemeStore } from '../composables/useThemeStore'

const props = defineProps<{ scope: 'user' | 'admin' }>()

const themeStore = useThemeStore(props.scope)
const isDark = computed(() => themeStore.isDark())
const toggleTheme = () => themeStore.toggleTheme()
</script>

<style scoped lang="less">
.theme-toggle {
  font-size: 16px;
  color: rgba(255, 255, 255, 0.65);
  &:hover {
    color: #fff !important;
    background: rgba(255, 255, 255, 0.08) !important;
  }
}
</style>
```

- [ ] **Step 3: 删除原 useTheme composable**

删除：`packages/shared/src/composables/useTheme.ts`（若目录已空，一并删除 composables 目录）

- [ ] **Step 4: 改写 admin-web App.vue**

`admin-web/src/App.vue`（完整覆盖）：
```vue
<template>
  <a-config-provider :theme="themeConfig">
    <router-view />
  </a-config-provider>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useThemeStore } from '@shared/web/composables/useThemeStore'

const themeStore = useThemeStore('admin')
const themeConfig = computed(() => themeStore.themeConfig())
</script>
```

- [ ] **Step 5: 删除 user-web 的 stores/theme.ts**

删除：`user-web/src/stores/theme.ts`

- [ ] **Step 6: 改写 user-web App.vue**

`user-web/src/App.vue`（完整覆盖）：
```vue
<template>
  <a-config-provider :theme="themeConfig">
    <router-view />
  </a-config-provider>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useThemeStore } from '@shared/web/composables/useThemeStore'

const themeStore = useThemeStore('user')
const themeConfig = computed(() => themeStore.themeConfig())
</script>
```

- [ ] **Step 7: 改写 user-web UserLayout.vue 主题部分**

修改 [user-web/src/layouts/UserLayout.vue](file:///d:/AI/DredgeAI/user-web/src/layouts/UserLayout.vue) 的 script 部分：

```ts
// 修改前
import { useThemeStore } from '@/stores/theme'
const themeStore = useThemeStore()
const isDark = computed(() => {
  if (themeStore.theme === 'auto') {
    return window.matchMedia('(prefers-color-scheme: dark)').matches
  }
  return themeStore.theme === 'dark'
})
function toggleTheme(): void {
  themeStore.theme = isDark.value ? 'light' : 'dark'
}

// 修改后
import { useThemeStore } from '@shared/web/composables/useThemeStore'
const themeStore = useThemeStore('user')
const isDark = computed(() => themeStore.isDark())
function toggleTheme(): void {
  themeStore.toggleTheme()
}
```

- [ ] **Step 8: 修复 user-web profile/index.vue 中的 themeStore 引用**

[user-web/src/views/profile/index.vue](file:///d:/AI/DredgeAI/user-web/src/views/profile/index.vue) 内所有 `themeStore.theme = xxx` 改为 `themeStore.theme.value = xxx`（因新 store 的 theme 是 ref，但通过 pinia 解构后可直接赋值，实际无需 .value，验证即可）。

确认 profile 页面切换主题的代码：`@click="themeStore.theme = opt.value"` 保持不变（pinia setup store 返回的 ref 在模板中可写）。

- [ ] **Step 9: 验证 typecheck + dev 双端切换主题**

运行：`pnpm typecheck && pnpm dev`
预期：
- typecheck 通过
- 双端切主题按钮可用，data-theme 属性切换正确，admin 的 ThemeToggle 与 user 的按钮都正常

- [ ] **Step 10: 提交**

```bash
git add packages/shared/src/web/composables/useThemeStore.ts packages/shared/src/web/components/ThemeToggle.vue packages/shared/src/composables admin-web/src/App.vue user-web/src/App.vue user-web/src/layouts/UserLayout.vue user-web/src/stores/theme.ts
git commit -m "refactor(theme): unify theme into shared useThemeStore with scope param, remove useTheme composable"
```

---

### Task 2.3: 抽取 useSidebarStore，简化双端 app store

**Files:**
- Create: `packages/shared/src/web/stores/useSidebarStore.ts`（替换占位）
- Modify: `user-web/src/stores/app.ts`（移除 sidebarCollapsed、toggleSidebar，改为引用 shared）
- Modify: `admin-web/src/stores/app.ts`（移除 sidebarCollapsed，改为引用 shared）
- Modify: `user-web/src/layouts/UserLayout.vue`（collapsed 改用 useSidebarStore）
- Modify: `admin-web/src/layouts/AdminLayout.vue`（collapsed 改用 useSidebarStore）

- [ ] **Step 1: 实现 useSidebarStore**

`packages/shared/src/web/stores/useSidebarStore.ts`（完整覆盖）：
```ts
import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface UseSidebarStoreOptions {
  scope: 'user' | 'admin'
}

/** 通用侧边栏折叠状态 store，避免双端各自维护 */
export function useSidebarStore(scope: UseSidebarStoreOptions['scope']) {
  const storeName = `sidebar-${scope}`
  const factory = defineStore(storeName, () => {
    const collapsed = ref(false)
    function toggle(): void {
      collapsed.value = !collapsed.value
    }
    function setCollapsed(v: boolean): void {
      collapsed.value = v
    }
    return { collapsed, toggle, setCollapsed }
  }, {
    persist: { pick: ['collapsed'] },
  })
  return factory()
}
```

- [ ] **Step 2: 改写 user-web app store**

`user-web/src/stores/app.ts`（完整覆盖）：
```ts
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { AppCard, TaskItem, FileItem } from '@/types'
import { getAppList } from '@/api/modules/app'
import { getRecentTasks, getQuickTasks } from '@/api/modules/task'
import { getRecentFiles } from '@/api/modules/file'
import { useSidebarStore } from '@shared/web/stores/useSidebarStore'

export interface SidebarApp {
  route: string
  title: string
  icon: string
}

/** user-web 业务 store：apps/tasks/files/visibleAppRoutes。
 *  sidebar 折叠状态已迁移至 @shared/web/stores/useSidebarStore。
 */
export const useAppStore = defineStore('app', () => {
  const apps = ref<AppCard[]>([])
  const tasks = ref<TaskItem[]>([])
  const quickTasks = ref<{ id: string; title: string; tag: string; route: string; icon: string }[]>([])
  const files = ref<FileItem[]>([])
  const visibleAppRoutes = ref<string[]>([])

  const sidebarStore = useSidebarStore('user')
  const sidebarCollapsed = computed(() => sidebarStore.collapsed)
  function toggleSidebar(): void { sidebarStore.toggle() }

  const authorizedApps = computed(() =>
    apps.value.filter((a) => a.status === '已授权')
  )

  const sidebarApps = computed(() =>
    authorizedApps.value
      .filter((a) => a.route && visibleAppRoutes.value.includes(a.route))
      .sort((a, b) => {
        const ai = visibleAppRoutes.value.indexOf(a.route!)
        const bi = visibleAppRoutes.value.indexOf(b.route!)
        return ai - bi
      })
      .map((a): SidebarApp => ({ route: a.route!, title: a.title, icon: a.icon }))
  )

  const sidebarAppsSet = computed(() => new Set(sidebarApps.value.map((a) => a.route)))

  function setVisibleRoutes(routes: string[]): void {
    visibleAppRoutes.value = routes
  }

  function toggleAppRoute(route: string): void {
    if (visibleAppRoutes.value.includes(route)) {
      visibleAppRoutes.value = visibleAppRoutes.value.filter((r) => r !== route)
    } else {
      visibleAppRoutes.value = [...visibleAppRoutes.value, route]
    }
  }

  function isRouteVisible(route: string): boolean {
    return sidebarAppsSet.value.has(route)
  }

  /** 默认勾选应用显示在侧边栏：通用3 + 施工2 + 经营2(情报采集子应用) */
  const DEFAULT_VISIBLE_ROUTES = ['/standard-query', '/ai-video', '/ai-dubbing', '/dredge-efficiency', '/bid-review', '/intelligence/dredge', '/intelligence/tech']

  async function fetchApps(): Promise<void> {
    apps.value = await getAppList()
    const routesWithRoute = authorizedApps.value
      .filter((a) => a.route)
      .map((a) => a.route!)

    visibleAppRoutes.value = visibleAppRoutes.value.filter((r) => routesWithRoute.includes(r))
    if (visibleAppRoutes.value.length === 0) {
      visibleAppRoutes.value = DEFAULT_VISIBLE_ROUTES.filter((r) => routesWithRoute.includes(r))
    }
  }
  async function fetchTasks(): Promise<void> { tasks.value = await getRecentTasks() }
  async function fetchQuickTasks(): Promise<void> { quickTasks.value = await getQuickTasks() }
  async function fetchFiles(): Promise<void> { files.value = await getRecentFiles() }

  return {
    apps, tasks, quickTasks, files,
    sidebarCollapsed, toggleSidebar,
    visibleAppRoutes, authorizedApps, sidebarApps,
    setVisibleRoutes, toggleAppRoute, isRouteVisible,
    fetchApps, fetchTasks, fetchQuickTasks, fetchFiles,
  }
}, {
  persist: { pick: ['visibleAppRoutes'] },
})
```

- [ ] **Step 3: 改写 admin-web app store**

`admin-web/src/stores/app.ts`（完整覆盖）：
```ts
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { useSidebarStore } from '@shared/web/stores/useSidebarStore'
import type { UserInfo } from '@/types'

/** admin-web 业务 store：profile。
 *  sidebar 折叠状态已迁移至 @shared/web/stores/useSidebarStore。
 */
export const useAppStore = defineStore('app', () => {
  const profile = ref<UserInfo | null>(null)

  const sidebarStore = useSidebarStore('admin')
  const sidebarCollapsed = computed(() => sidebarStore.collapsed)
  function toggleSidebar(): void { sidebarStore.toggle() }

  const isSuperAdmin = computed(() => profile.value?.role === 'super_admin')

  function setProfile(user: UserInfo): void {
    profile.value = user
  }

  return {
    sidebarCollapsed, toggleSidebar,
    profile, isSuperAdmin, setProfile,
  }
})
```

- [ ] **Step 4: 验证 typecheck + dev 双端折叠侧边栏正常**

运行：`pnpm typecheck && pnpm dev`
预期：
- 双端折叠按钮工作正常
- 刷新后折叠状态保持（persist 通过 sidebar-{user|admin} store 保存）

- [ ] **Step 5: 提交**

```bash
git add packages/shared/src/web/stores/useSidebarStore.ts user-web/src/stores/app.ts admin-web/src/stores/app.ts
git commit -m "refactor(stores): extract useSidebarStore to shared, simplify both app stores"
```

---

## Phase 3: 应用 manifest 机制

### Task 3.1: 创建 user-web 应用 manifest 注册表

**Files:**
- Create: `user-web/src/apps/index.ts`（manifest 注册表）
- Create: `user-web/src/apps/standard-query.ts`
- Create: `user-web/src/apps/bid-review.ts`
- Create: `user-web/src/apps/ai-video.ts`
- Create: `user-web/src/apps/ai-dubbing.ts`
- Create: `user-web/src/apps/design-experience.ts`
- Create: `user-web/src/apps/construction-experience.ts`
- Create: `user-web/src/apps/construction-review.ts`
- Create: `user-web/src/apps/dredge-efficiency.ts`
- Create: `user-web/src/apps/intelligence-dredge.ts`
- Create: `user-web/src/apps/intelligence-tech.ts`

- [ ] **Step 1: 创建各应用 manifest 文件**

每个文件结构相同，以 `user-web/src/apps/standard-query.ts` 为例：
```ts
import type { AppManifest } from '@shared/core/types'

/** 标准查询应用 manifest */
export const standardQueryManifest: AppManifest = {
  id: '1',
  route: '/standard-query',
  name: 'StandardQuery',
  title: '标准查询',
  icon: 'BookOutlined',
  component: () => import('@/views/standards/index.vue'),
  defaultVisible: true,
  category: '通用',
}
```

其余应用同理（route/name/title/icon 按 router/index.ts 现有值填充），以下逐一给出关键参数：

| 文件 | id | route | name | title | icon | defaultVisible | category |
|---|---|---|---|---|---|---|---|
| bid-review.ts | 9 | /bid-review | BidReview | 投标审核 | FileSearchOutlined | true | 经营 |
| ai-video.ts | 2 | /ai-video | AiVideo | AI视频 | VideoCameraOutlined | true | 通用 |
| ai-dubbing.ts | 3 | /ai-dubbing | AiDubbing | AI配音 | CustomerServiceOutlined | true | 通用 |
| design-experience.ts | 4 | /design-experience | DesignExperience | 设计经验 | BulbOutlined | false | 设计 |
| construction-experience.ts | 5 | /construction-experience | ConstructionExperience | 施工经验 | ToolOutlined | false | 施工 |
| construction-review.ts | 6 | /construction-review | ConstructionReview | 施组审核 | FileProtectOutlined | false | 施工 |
| dredge-efficiency.ts | 7 | /dredge-efficiency | DredgeEfficiency | 耙吸效率 | DashboardOutlined | true | 施工 |
| intelligence-dredge.ts | 8-1 | /intelligence/dredge | IntelligenceDredge | 疏浚情报 | RadarChartOutlined | true | 经营 |
| intelligence-tech.ts | 8-2 | /intelligence/tech | IntelligenceTech | 科技情报 | ExperimentOutlined | true | 经营 |

对于尚未实现的页面，`component` 指向 `@/views/placeholder/PlaceholderView.vue`。

- [ ] **Step 2: 创建 manifest 注册表 barrel**

`user-web/src/apps/index.ts`：
```ts
import type { AppManifest } from '@shared/core/types'
import { standardQueryManifest } from './standard-query'
import { bidReviewManifest } from './bid-review'
import { aiVideoManifest } from './ai-video'
import { aiDubbingManifest } from './ai-dubbing'
import { designExperienceManifest } from './design-experience'
import { constructionExperienceManifest } from './construction-experience'
import { constructionReviewManifest } from './construction-review'
import { dredgeEfficiencyManifest } from './dredge-efficiency'
import { intelligenceDredgeManifest } from './intelligence-dredge'
import { intelligenceTechManifest } from './intelligence-tech'

/** 所有可用应用 manifest（按 id 索引） */
export const appManifests: AppManifest[] = [
  standardQueryManifest,
  bidReviewManifest,
  aiVideoManifest,
  aiDubbingManifest,
  designExperienceManifest,
  constructionExperienceManifest,
  constructionReviewManifest,
  dredgeEfficiencyManifest,
  intelligenceDredgeManifest,
  intelligenceTechManifest,
]

/** 按 id 查找 manifest */
export function findManifest(id: string): AppManifest | undefined {
  return appManifests.find((m) => m.id === id)
}

/** 按 route 查找 manifest */
export function findManifestByRoute(route: string): AppManifest | undefined {
  return appManifests.find((m) => m.route === route)
}
```

- [ ] **Step 3: 验证 typecheck 通过**

运行：`pnpm --filter user-web typecheck`
预期：通过。

- [ ] **Step 4: 提交**

```bash
git add user-web/src/apps
git commit -m "feat(user-web): add per-app AppManifest registry"
```

---

### Task 3.2: 改写 user-web 路由为 manifest 驱动

**Files:**
- Modify: `user-web/src/router/index.ts`

- [ ] **Step 1: 改写路由表**

`user-web/src/router/index.ts`（完整覆盖）：
```ts
import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'
import UserLayout from '@/layouts/UserLayout.vue'
import { appManifests } from '@/apps'

/** 由 manifest 动态生成子路由（包含固定路由 dashboard/profile/api） */
function buildAppRoutes(): RouteRecordRaw[] {
  return appManifests.map((m) => ({
    path: m.route.slice(1), // 去掉前导 /
    name: m.name,
    component: m.component,
    meta: { title: m.title, appId: m.id },
  }))
}

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      component: UserLayout,
      redirect: '/dashboard',
      children: [
        { path: 'dashboard', name: 'UserDashboard', component: () => import('@/views/dashboard/index.vue'), meta: { title: '工作台' } },
        ...buildAppRoutes(),
        { path: 'profile', name: 'Profile', component: () => import('@/views/profile/index.vue'), meta: { title: '个人中心' } },
        { path: 'api', name: 'ApiManage', component: () => import('@/views/api/index.vue'), meta: { title: 'API 管理' } },
      ],
    },
  ],
})

router.afterEach((to) => {
  const title = to.meta.title as string | undefined
  document.title = title ? `${title} · 智浚 AI` : '智浚 AI · 用户端'
})

export default router
```

- [ ] **Step 2: 验证 dev 双端运行**

运行：`pnpm dev`
预期：访问 http://localhost:5373，所有应用路由（如 /standard-query, /bid-review）正常跳转。

- [ ] **Step 3: 提交**

```bash
git add user-web/src/router/index.ts
git commit -m "refactor(user-web): generate routes from app manifests"
```

---

### Task 3.3: admin 发布状态影响 user-web 可见性

**Files:**
- Modify: `user-web/src/stores/app.ts`（fetchApps 时按 manifest 过滤路由）
- Modify: `user-web/src/layouts/UserLayout.vue`（菜单项使用 manifest 数据）

> **现状：** `fetchApps` 已经基于 `authorizedApps`（status === '已授权'）过滤。这一步主要确保 manifest 与 appCards 的一致性，并使 admin 下架某应用后 user 端不可见。

- [ ] **Step 1: 改写 fetchApps，结合 manifest 与 authorizedApps**

修改 [user-web/src/stores/app.ts](file:///d:/AI/DredgeAI/user-web/src/stores/app.ts) 的 `fetchApps` 函数：

```ts
import { appManifests } from '@/apps'

// ...

async function fetchApps(): Promise<void> {
  const remoteApps = await getAppList()
  // 仅保留在 manifest 中存在的应用（即 admin 已发布且 user 端有对应实现的）
  const manifestRoutes = new Set(appManifests.map((m) => m.route))
  apps.value = remoteApps.filter((a) => a.route && manifestRoutes.has(a.route))

  const routesWithRoute = authorizedApps.value
    .filter((a) => a.route)
    .map((a) => a.route!)

  visibleAppRoutes.value = visibleAppRoutes.value.filter((r) => routesWithRoute.includes(r))
  if (visibleAppRoutes.value.length === 0) {
    visibleAppRoutes.value = DEFAULT_VISIBLE_ROUTES.filter((r) => routesWithRoute.includes(r))
  }
}
```

- [ ] **Step 2: 改写 UserLayout 菜单图标使用 manifest**

修改 [user-web/src/layouts/UserLayout.vue](file:///d:/AI/DredgeAI/user-web/src/layouts/UserLayout.vue) 的菜单渲染部分，使用 `findManifestByRoute` 获取图标：

```ts
import { findManifestByRoute } from '@/apps'
import * as Icons from '@ant-design/icons-vue'

const iconComp = (route: string) => {
  const m = findManifestByRoute(route)
  if (!m) return null
  return (Icons as Record<string, unknown>)[m.icon]
}
```

模板里把 `iconMap[item.route]` 改为 `iconComp(item.route)`，删除原硬编码的 iconMap 对象。

- [ ] **Step 3: 验证 dev 双端运行**

运行：`pnpm dev`
预期：user-web 侧边栏显示 manifest 中声明的图标，admin 下架某应用后 user 刷新可见性变化。

- [ ] **Step 4: 提交**

```bash
git add user-web/src/stores/app.ts user-web/src/layouts/UserLayout.vue
git commit -m "feat(user-web): admin publish status drives user-web app visibility via manifest"
```

---

## Phase 4: 清理视图层 mock 与死代码

### Task 4.1: user-web/api/index.vue 改用 API 层

**Files:**
- Modify: `user-web/src/views/api/index.vue`

- [ ] **Step 1: 删除视图内 mock 数据，改用 api/modules**

打开 [user-web/src/views/api/index.vue](file:///d:/AI/DredgeAI/user-web/src/views/api/index.vue)，删除所有形如 `mockKeys`、`mockChartData`、`makeMockSeries` 的本地常量与函数。

script 改为：
```ts
import { ref, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import SectionCard from '@shared/components/SectionCard.vue'
import ChartContainer from '@shared/components/ChartContainer.vue'
import { useCssVar } from '@shared/composables/useCssVar'
import {
  getApiKeyList, getModelTypes, getUsageByModel, getUsageByKey,
  getUsageStats, getUsageTimeSeries,
} from '@/api/modules/apikey'
import type { ApiKey, ModelType, UsageByModel, UsageByKey, ApiUsageStats, UsageTimeSeries } from '@/types'

const loading = ref(false)
const chartLoading = ref(false)
const range = ref<'7d' | '30d' | '90d'>('30d')
const rangeOptions = [
  { label: '近 7 天', value: '7d' as const },
  { label: '近 30 天', value: '30d' as const },
  { label: '近 90 天', value: '90d' as const },
]

const apiKeys = ref<ApiKey[]>([])
const modelTypes = ref<ModelType[]>([])
const usageByModel = ref<UsageByModel[]>([])
const usageByKey = ref<UsageByKey[]>([])
const usageStats = ref<ApiUsageStats>({ totalTokens: 0, totalCalls: 0 })
const timeSeries = ref<UsageTimeSeries>({ categories: [], byModel: [], byKey: [], byName: [] })

const brandColor = useCssVar('--color-brand')
const successColor = useCssVar('--color-success')

async function loadAll(): Promise<void> {
  loading.value = true
  try {
    const [keys, models, byModel, byKey, stats] = await Promise.all([
      getApiKeyList(),
      getModelTypes(),
      getUsageByModel(),
      getUsageByKey(),
      getUsageStats(),
    ])
    apiKeys.value = keys
    modelTypes.value = models
    usageByModel.value = byModel
    usageByKey.value = byKey
    usageStats.value = stats
  } catch (e) {
    message.error((e as Error).message || '加载失败')
  } finally {
    loading.value = false
  }
}

async function loadTimeSeries(): Promise<void> {
  chartLoading.value = true
  try {
    timeSeries.value = await getUsageTimeSeries(range.value)
  } catch (e) {
    message.error((e as Error).message || '趋势加载失败')
  } finally {
    chartLoading.value = false
  }
}

onMounted(() => {
  loadAll()
  loadTimeSeries()
})

// 计算 option 时使用 timeSeries.value.byModel / byKey / byName
const callsChartOption = computed(() => ({
  tooltip: { trigger: 'axis' as const },
  legend: { data: timeSeries.value.byName.map((s) => s.name) },
  xAxis: { type: 'category' as const, data: timeSeries.value.categories },
  yAxis: { type: 'value' as const },
  series: timeSeries.value.byName.map((s) => ({
    name: s.name, type: 'line' as const, smooth: true, data: s.data,
  })),
}))
</script>
```

> **注意：** 模板部分需保持原 echarts 渲染结构，仅替换数据源为上面的 ref。

- [ ] **Step 2: 验证 typecheck + dev API 页面正常**

运行：`pnpm typecheck && pnpm dev`
预期：访问 http://localhost:5373/api，图表与列表正常显示，数据来自 mock（通过 API 层）。

- [ ] **Step 3: 提交**

```bash
git add user-web/src/views/api/index.vue
git commit -m "refactor(user-web): api page consumes API layer instead of inline mock"
```

---

### Task 4.2: admin-web/api/index.vue 改用 API 层

**Files:**
- Modify: `admin-web/src/views/api/index.vue`

- [ ] **Step 1: 同 Task 4.1 模式改写 admin-web api 视图**

参照 Task 4.1 的方式，删除 [admin-web/src/views/api/index.vue](file:///d:/AI/DredgeAI/admin-web/src/views/api/index.vue) 内的本地 mock 常量，改为调用 `@/api/modules/apikey` 中的函数。useCssVar 已用 composable，保持不变。

- [ ] **Step 2: 验证 typecheck + dev**

运行：`pnpm typecheck && pnpm dev`
预期：访问 http://localhost:5374/api，页面正常。

- [ ] **Step 3: 提交**

```bash
git add admin-web/src/views/api/index.vue
git commit -m "refactor(admin-web): api page consumes API layer instead of inline mock"
```

---

### Task 4.3: 清理 mock/index.ts 的 console.log

**Files:**
- Modify: `user-web/src/mock/index.ts`
- Modify: `admin-web/src/mock/index.ts`

- [ ] **Step 1: 删除 console.log/console.warn**

[user-web/src/mock/index.ts](file:///d:/AI/DredgeAI/user-web/src/mock/index.ts) 与 [admin-web/src/mock/index.ts](file:///d:/AI/DredgeAI/admin-web/src/mock/index.ts) 中所有 `console.log('[mock]...')` 与 `console.warn('[mock]...')` 删除（包括 onAny 兜底中的）。

- [ ] **Step 2: 验证 dev**

运行：`pnpm dev`
预期：浏览器控制台无 `[mock]` 输出，页面正常。

- [ ] **Step 3: 提交**

```bash
git add user-web/src/mock/index.ts admin-web/src/mock/index.ts
git commit -m "chore(mock): remove debug console logs from mock setup"
```

---

### Task 4.4: USE_MOCK 改为环境感知

**Files:**
- Modify: `user-web/src/utils/constants.ts`
- Modify: `admin-web/src/utils/constants.ts`

- [ ] **Step 1: 改 USE_MOCK 为开发环境感知**

[user-web/src/utils/constants.ts](file:///d:/AI/DredgeAI/user-web/src/utils/constants.ts) 与 [admin-web/src/utils/constants.ts](file:///d:/AI/DredgeAI/admin-web/src/utils/constants.ts) 中：

```ts
// 修改前
export const USE_MOCK = true

// 修改后
export const USE_MOCK = import.meta.env.DEV && import.meta.env.VITE_USE_MOCK !== 'false'
```

> **说明：** 默认开发环境启用 mock；若需在 dev 中调试真实接口，可在 .env.local 设置 `VITE_USE_MOCK=false`。生产构建（import.meta.env.DEV === false）永远禁用 mock。

- [ ] **Step 2: 验证 dev 与 build**

运行：`pnpm dev` → mock 仍启用，页面正常
运行：`pnpm build` → 构建成功，mock 代码被 tree-shake（确认 mock/index.ts 不在产物中）

- [ ] **Step 3: 提交**

```bash
git add user-web/src/utils/constants.ts admin-web/src/utils/constants.ts
git commit -m "fix(mock): make USE_MOCK dev-only to prevent production mock leakage"
```

---

## Phase 5: 基础设施（路由守卫 + 权限指令 + ErrorBoundary）

### Task 5.1: 路由守卫（鉴权 + 应用可见性）

**Files:**
- Create: `user-web/src/router/guards.ts`
- Modify: `user-web/src/router/index.ts`
- Create: `admin-web/src/router/guards.ts`
- Modify: `admin-web/src/router/index.ts`

- [ ] **Step 1: 创建 user-web 路由守卫**

`user-web/src/router/guards.ts`：
```ts
import type { Router } from 'vue-router'
import { useAppStore } from '@/stores/app'
import { findManifestByRoute } from '@/apps'

/** 安装 user-web 路由守卫：未授权应用跳转工作台 */
export function installUserGuards(router: Router): void {
  router.beforeEach((to, _from, next) => {
    const appId = to.meta.appId as string | undefined
    if (!appId) return next() // dashboard/profile/api 等固定路由

    const appStore = useAppStore()
    const manifest = findManifestByRoute(to.path)
    if (!manifest) return next('/dashboard')

    // 应用必须在 authorizedApps 且 visibleAppRoutes 中
    const authorized = appStore.authorizedApps.some((a) => a.route === to.path)
    const visible = appStore.isRouteVisible(to.path)
    if (!authorized || !visible) {
      return next('/dashboard')
    }
    next()
  })
}
```

- [ ] **Step 2: 在 user-web router/index.ts 安装守卫**

修改 [user-web/src/router/index.ts](file:///d:/AI/DredgeAI/user-web/src/router/index.ts)，在 `export default router` 前加：
```ts
import { installUserGuards } from './guards'
installUserGuards(router)
```

- [ ] **Step 3: 创建 admin-web 路由守卫（仅 token 校验）**

`admin-web/src/router/guards.ts`：
```ts
import type { Router } from 'vue-router'
import { STORAGE_TOKEN_KEY } from '@/utils/constants'

/** 安装 admin-web 路由守卫：未登录跳转登录页（暂未实现登录页，先占位） */
export function installAdminGuards(router: Router): void {
  router.beforeEach((to, _from, next) => {
    const token = localStorage.getItem(STORAGE_TOKEN_KEY)
    if (!token && to.path !== '/login') {
      // 项目当前未实现登录页，先放行，Task 5.4 接入真实登录后启用
      return next()
    }
    next()
  })
}
```

- [ ] **Step 4: 在 admin-web router/index.ts 安装守卫**

修改 [admin-web/src/router/index.ts](file:///d:/AI/DredgeAI/admin-web/src/router/index.ts)，在 `export default router` 前加：
```ts
import { installAdminGuards } from './guards'
installAdminGuards(router)
```

- [ ] **Step 5: 验证 typecheck + dev**

运行：`pnpm typecheck && pnpm dev`
预期：双端正常，user-web 直接访问未授权应用路由会被重定向到 /dashboard。

- [ ] **Step 6: 提交**

```bash
git add user-web/src/router admin-web/src/router
git commit -m "feat(router): add route guards for auth and app visibility"
```

---

### Task 5.2: v-permission 指令

**Files:**
- Modify: `packages/shared/src/web/directives/permission.ts`（替换占位）
- Modify: `packages/shared/src/index.ts`（已在 Task 2.1 导出）
- Modify: `user-web/src/main.ts`（注册指令）
- Modify: `admin-web/src/main.ts`（注册指令）

- [ ] **Step 1: 实现权限指令**

`packages/shared/src/web/directives/permission.ts`（完整覆盖）：
```ts
import type { Directive, DirectiveBinding } from 'vue'

/** 权限解析器：返回当前用户拥有的权限码列表 */
export type PermissionResolver = () => string[]

let resolver: PermissionResolver = () => []

/** 配置全局权限解析器（在 main.ts 中调用一次） */
export function configurePermissionResolver(r: PermissionResolver): void {
  resolver = r
}

/** v-permission="'apikey:create'" 或 v-permission="['apikey:create','apikey:delete']" */
export const permissionDirective: Directive<HTMLElement, string | string[]> = {
  mounted(el: HTMLElement, binding: DirectiveBinding<string | string[]>) {
    const required = Array.isArray(binding.value) ? binding.value : [binding.value]
    const granted = new Set(resolver())
    const hasAny = required.some((p) => granted.has(p))
    if (!hasAny) {
      el.parentNode?.removeChild(el)
    }
  },
}
```

- [ ] **Step 2: 在双端 main.ts 注册指令**

[user-web/src/main.ts](file:///d:/AI/DredgeAI/user-web/src/main.ts) 与 [admin-web/src/main.ts](file:///d:/AI/DredgeAI/admin-web/src/main.ts) 中：

```ts
import { permissionDirective, configurePermissionResolver } from '@shared/web/directives/permission'

const app = createApp(App)
app.directive('permission', permissionDirective)

// 暂用空解析器（实际权限接口未实现，所有元素都保留）
configurePermissionResolver(() => [])
```

- [ ] **Step 3: 验证 typecheck + dev**

运行：`pnpm typecheck && pnpm dev`
预期：双端正常运行，无指令报错。

- [ ] **Step 4: 提交**

```bash
git add packages/shared/src/web/directives user-web/src/main.ts admin-web/src/main.ts
git commit -m "feat(shared): add v-permission directive with configurable resolver"
```

---

### Task 5.3: ErrorBoundary 组件

**Files:**
- Modify: `packages/shared/src/web/components/ErrorBoundary.vue`（替换占位）
- Modify: `user-web/src/router/index.ts`（路由级 ErrorBoundary 包装）
- Modify: `admin-web/src/router/index.ts`

- [ ] **Step 1: 实现 ErrorBoundary 组件**

`packages/shared/src/web/components/ErrorBoundary.vue`（完整覆盖）：
```vue
<template>
  <slot v-if="!error" />
  <div v-else class="error-boundary">
    <a-result status="error" title="页面加载失败" :sub-title="errorMessage">
      <template #extra>
        <a-button type="primary" @click="reset">重试</a-button>
        <a-button @click="goHome">返回首页</a-button>
      </template>
    </a-result>
  </div>
</template>

<script setup lang="ts">
import { ref, onErrorCaptured } from 'vue'
import { useRouter } from 'vue-router'

const error = ref<Error | null>(null)
const errorMessage = ref('')

onErrorCaptured((err: Error) => {
  error.value = err
  errorMessage.value = err.message || '未知错误'
  // 阻止错误继续向上传播
  return false
})

const router = useRouter()
function reset(): void {
  error.value = null
  errorMessage.value = ''
}
function goHome(): void {
  error.value = null
  router.push('/')
}
</script>

<style scoped lang="less">
@import '../styles/variables.less';

.error-boundary {
  padding: @spacing-2xl;
  display: flex;
  justify-content: center;
}
</style>
```

- [ ] **Step 2: 在 UserLayout/AdminLayout 的 router-view 外包 ErrorBoundary**

修改 [user-web/src/layouts/UserLayout.vue](file:///d:/AI/DredgeAI/user-web/src/layouts/UserLayout.vue) 模板：
```vue
<!-- 修改前 -->
<router-view />

<!-- 修改后 -->
<ErrorBoundary>
  <router-view />
</ErrorBoundary>
```

script 中引入：
```ts
import ErrorBoundary from '@shared/components/ErrorBoundary.vue'
```

admin-web 同理修改 [admin-web/src/layouts/AdminLayout.vue](file:///d:/AI/DredgeAI/admin-web/src/layouts/AdminLayout.vue)。

- [ ] **Step 3: 验证 typecheck + dev**

运行：`pnpm typecheck && pnpm dev`
预期：双端正常，故意制造一个组件抛错（临时在某个视图 `throw new Error('test')`）可看到 ErrorBoundary 兜底，验证后回滚。

- [ ] **Step 4: 提交**

```bash
git add packages/shared/src/web/components/ErrorBoundary.vue user-web/src/layouts/UserLayout.vue admin-web/src/layouts/AdminLayout.vue
git commit -m "feat(shared): add ErrorBoundary component, wrap router-view in both apps"
```

---

### Task 5.4: axios 全局错误兜底（message 提示）

**Files:**
- Modify: `user-web/src/api/request.ts`
- Modify: `admin-web/src/api/request.ts`

- [ ] **Step 1: 在拦截器错误分支统一弹出 message**

[user-web/src/api/request.ts](file:///d:/AI/DredgeAI/user-web/src/api/request.ts) 与 [admin-web/src/api/request.ts](file:///d:/AI/DredgeAI/admin-web/src/api/request.ts) 的响应错误拦截器改为：

```ts
import { message } from 'ant-design-vue'

instance.interceptors.response.use(
  (resp) => { nprogress.done(); return resp },
  (error) => {
    nprogress.done()
    const abpError = error.response?.data?.error
    const msg = abpError?.message || error.message || '请求失败'
    // 401 不弹 message（由 onUnauthorized 处理）
    if (error.response?.status !== 401) {
      message.error(msg)
    }
    return Promise.reject(error)
  },
)
```

- [ ] **Step 2: 验证 dev 模拟 500 错误**

临时修改某 mock 路由返回 500，确认页面顶部出现错误提示，验证后回滚。

- [ ] **Step 3: 提交**

```bash
git add user-web/src/api/request.ts admin-web/src/api/request.ts
git commit -m "feat(api): show global error message on non-401 request failures"
```

---

## Phase 6: i18n 骨架预留

### Task 6.1: 引入 vue-i18n 骨架（不全量替换）

**Files:**
- Modify: `user-web/package.json`（添加 vue-i18n 依赖）
- Modify: `admin-web/package.json`
- Create: `packages/shared/src/web/i18n/index.ts`
- Create: `packages/shared/src/web/i18n/zh-CN.ts`
- Create: `packages/shared/src/web/i18n/en-US.ts`
- Modify: `user-web/src/main.ts`
- Modify: `admin-web/src/main.ts`

- [ ] **Step 1: 安装 vue-i18n**

运行：
```bash
pnpm --filter user-web add vue-i18n@9
pnpm --filter admin-web add vue-i18n@9
```

- [ ] **Step 2: 创建 i18n 工厂与默认词条**

`packages/shared/src/web/i18n/zh-CN.ts`：
```ts
export default {
  common: {
    confirm: '确认',
    cancel: '取消',
    save: '保存',
    delete: '删除',
    edit: '编辑',
    loading: '加载中...',
    empty: '暂无数据',
  },
  theme: {
    light: '亮色',
    dark: '暗色',
    auto: '跟随系统',
  },
}
```

`packages/shared/src/web/i18n/en-US.ts`：
```ts
export default {
  common: {
    confirm: 'Confirm',
    cancel: 'Cancel',
    save: 'Save',
    delete: 'Delete',
    edit: 'Edit',
    loading: 'Loading...',
    empty: 'No data',
  },
  theme: {
    light: 'Light',
    dark: 'Dark',
    auto: 'Auto',
  },
}
```

`packages/shared/src/web/i18n/index.ts`：
```ts
import { createI18n } from 'vue-i18n'
import zhCN from './zh-CN'
import enUS from './en-US'

export type AppLocale = 'zh-CN' | 'en-US'

/** 创建共享 i18n 实例，各端 main.ts 中 app.use(install) */
export function createAppI18n(initialLocale: AppLocale = 'zh-CN') {
  return createI18n({
    legacy: false,
    locale: initialLocale,
    fallbackLocale: 'zh-CN',
    messages: {
      'zh-CN': zhCN,
      'en-US': enUS,
    },
  })
}
```

- [ ] **Step 3: 在双端 main.ts 注册 i18n**

[user-web/src/main.ts](file:///d:/AI/DredgeAI/user-web/src/main.ts) 与 [admin-web/src/main.ts](file:///d:/AI/DredgeAI/admin-web/src/main.ts)：
```ts
import { createAppI18n } from '@shared/web/i18n'

const i18n = createAppI18n('zh-CN')
app.use(i18n)
```

- [ ] **Step 4: 验证 typecheck + dev**

运行：`pnpm typecheck && pnpm dev`
预期：双端正常，i18n 已挂载但暂不替换任何已有中文（仅预留骨架，未来逐步迁移）。

- [ ] **Step 5: 提交**

```bash
git add packages/shared/src/web/i18n user-web/package.json admin-web/package.json user-web/src/main.ts admin-web/src/main.ts pnpm-lock.yaml
git commit -m "feat(i18n): add vue-i18n skeleton with zh-CN/en-US dictionaries"
```

---

## 完工验收

### 验收清单

- [ ] `pnpm typecheck` 双端全部通过
- [ ] `pnpm dev` 双端启动无控制台报错
- [ ] `pnpm build` 双端构建成功
- [ ] user-web 所有应用路由可访问
- [ ] admin-web 发布/下架应用后 user-web 刷新可见性变化
- [ ] 双端主题切换正常，刷新后保持
- [ ] 双端侧边栏折叠状态刷新后保持
- [ ] 故意制造接口 500，ErrorBoundary + message 兜底
- [ ] 生产构建产物不含 mock 代码（USE_MOCK 为 false）

### 重构成果对照

| 原痛点 | 解决方式 | 验证 Task |
|---|---|---|
| shared 未分层 | core/web 分目录 | Task 1.1, 2.1 |
| 路由硬编码 | manifest 驱动 | Task 3.1-3.3 |
| 视图硬编码 mock | 改用 API 层 | Task 4.1-4.2 |
| request.ts 重复 | createRequest 工厂 | Task 1.3 |
| 主题系统分裂 | 统一 useThemeStore(scope) | Task 2.2 |
| store 命名冲突 | 抽 useSidebarStore | Task 2.3 |
| types 巨石 | 按领域拆分 | Task 1.2 |
| API 路径不统一 | URL 契约 + 统一 apikey | Task 1.4 |
| 缺路由守卫 | installXxxGuards | Task 5.1 |
| 缺权限指令 | v-permission | Task 5.2 |
| 缺错误边界 | ErrorBoundary + 拦截器 | Task 5.3-5.4 |
| USE_MOCK 硬编码 true | import.meta.env.DEV 感知 | Task 4.4 |
| console.log 残留 | 删除 | Task 4.3 |

---

## 执行选择

**Plan complete and saved to `docs/superpowers/plans/2026-07-19-shared-refactor.md`. Two execution options:**

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
