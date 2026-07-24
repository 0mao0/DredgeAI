# DredgeAI 代码质量审计报告

> **日期：** 2026-07-24
> **范围：** 代码结构、代码质量、架构一致性
> **目标：** 建立可持续的模式，避免后续重构和新模块大规模重写

---

## 概述

经过对 169 个源文件（113 `.ts` + 56 `.vue`，约 11,838 行）的系统审计，项目整体架构良好 —— 分层清晰（core/web/mock）、严格 TypeScript、共享模式成熟。问题集中在：上次重构的 4 个收尾任务未完成、admin-web 路由模式与 user-web 不一致、两处配置重复、错误处理缺乏统一模式、以及少量死代码和类型瑕疵。

**优先级定义：**
- **P0**：阻塞新开发或造成重复劳动
- **P1**：近期应做，防止技术债累积
- **P2**：可排期，不影响日常开发
- **P3**：锦上添花

**工作量定义：**
- **XS**（<1h）| **S**（1-3h）| **M**（1天）| **L**（2-3天）

---

## 一、完成遗留重构任务

来源：`docs/superpowers/plans/2026-07-19-shared-refactor.md`

### 1.1 移除 API 页面中的 inline mock 数据

| 属性 | 值 |
|------|-----|
| **优先级** | P1 |
| **工作量** | S（各项 1-2h） |

**问题：** 两个 API 管理页面仍在视图层硬编码 mock 数据，绕过 API 层，与项目其他模块的模式不一致。

**user-web `src/views/api/index.vue`（第 142-148 行）：**
```ts
const mockKeys = [
  { id: '1', name: '生产环境-主入口', key: 'sk-dg-xxxxxxxxxxxx1', model: 'GPT-4o', createdAt: '2026-06-01', docUrl: 'https://docs.example.com/gpt4o' },
  { id: '2', name: '生产环境-备用', key: 'sk-dg-xxxxxxxxxxxx2', model: 'GPT-4o-mini', createdAt: '2026-06-10', docUrl: 'https://docs.example.com/gpt4o-mini' },
  { id: '3', name: '测试环境', key: 'sk-dg-xxxxxxxxxxxx3', model: 'Claude-3.5-Sonnet', createdAt: '2026-06-15', docUrl: 'https://docs.example.com/claude35' },
  { id: '4', name: '内部工具-AI助手', key: 'sk-dg-xxxxxxxxxxxx4', model: 'DeepSeek-V3', createdAt: '2026-06-20', docUrl: 'https://docs.example.com/deepseek' },
  { id: '5', name: '数据分析管道', key: 'sk-dg-xxxxxxxxxxxx5', model: 'GPT-4o', createdAt: '2026-07-01', docUrl: 'https://docs.example.com/gpt4o' },
]
```

**admin-web `src/views/api/index.vue`（第 239 行起）：**
- `models.value = mockModels` 直接引用视图层常量
- `makeMockSeries` 函数在视图内拼装图表数据

**建议：** 两个页面改为调用各自的 API 模块（`@/api/modules/apikey`），数据获取逻辑统一走 `request → mock routes → shared mock data` 链路。这与项目中 bid、dubbing、standards 等模块的模式一致。

---

### 1.2 清理 mock/index.ts 中的调试日志

| 属性 | 值 |
|------|-----|
| **优先级** | P1 |
| **工作量** | XS（15min） |

**问题：** 两个 app 的 `src/mock/index.ts` 中均有调试用的 `console.log` / `console.warn`：

- `user-web/src/mock/index.ts` — 第 18、25、46、52、72 行，共 5 处
- `admin-web/src/mock/index.ts` — 第 15、22、42、48 行，共 4 处

**建议：** 全部删除。这些日志在开发期有用，但已是遗留调试代码。如确实需要 mock 命中的调试能力，可改为条件编译：`if (import.meta.env.DEV) console.log(...)`。

---

### 1.3 USE_MOCK 增加环境变量控制

| 属性 | 值 |
|------|-----|
| **优先级** | P2 |
| **工作量** | XS（15min） |

**问题：** 当前 `USE_MOCK` 仅检查 `import.meta.env.DEV`。当需要在 dev 模式下调试真实 API 时，只能改代码，无法通过环境变量切换。

**当前代码（`user-web/src/utils/constants.ts:8` / `admin-web/src/utils/constants.ts:2`）：**
```ts
export const USE_MOCK = import.meta.env.DEV
```

**建议改为：**
```ts
export const USE_MOCK = import.meta.env.DEV && import.meta.env.VITE_USE_MOCK !== 'false'
```

这样在 `.env.local` 中设置 `VITE_USE_MOCK=false` 即可临时关闭 mock 调试真实 API，无需改代码。

---

### 1.4 request.ts 添加全局错误 toast

| 属性 | 值 |
|------|-----|
| **优先级** | P1 |
| **工作量** | S（1h） |

**问题：** 两个 app 的 `request.ts` 响应错误拦截器只调用了 `nprogress.done()`，没有向用户反馈错误信息。当前任何 API 失败都是静默的，用户不知道发生了什么。

**当前代码（`user-web/src/api/request.ts:21-24` / `admin-web/src/api/request.ts:19-22`）：**
```ts
instance.interceptors.response.use(
  (resp) => { nprogress.done(); return resp },
  (err) => { nprogress.done(); return Promise.reject(err) },
)
```

**建议改为：**
```ts
import { message } from 'ant-design-vue'

instance.interceptors.response.use(
  (resp) => { nprogress.done(); return resp },
  (error) => {
    nprogress.done()
    if (error.response?.status !== 401) {
      const msg = error.response?.data?.error?.message || error.message || '请求失败'
      message.error(msg)
    }
    return Promise.reject(error)
  },
)
```

**注意：** 由于两个 `request.ts` 几乎相同（见本文第三节），建议先在 shared 中创建 `createWebRequest()`，再在其中统一添加错误 toast。

---

## 二、统一 admin-web 路由为 manifest 模式

| 属性 | 值 |
|------|-----|
| **优先级** | P1 |
| **工作量** | M（1 天） |

**问题：** `admin-web/src/router/index.ts` 中路由全部硬编码（47 行路由定义），而 `user-web` 已经完全迁移到 `AppManifest` + `manifestToRoutes()` 的声明式模式（`user-web/src/router/manifests.ts` 133 行）。

**影响：**
1. 新增管理页面需手动修改 `router/index.ts`，而 user-web 只需追加 manifest 条目
2. `AdminLayout.vue` 中的 `routeParentsMap`（第 233-249 行）与路由定义重复维护，修改路由时容易遗漏
3. 两个 app 的路由模式不一致，新成员容易困惑

**建议：**
1. 在 `admin-web/src/router/` 下创建 `manifests.ts`，将路由声明为 `AppManifest[]`
2. 改造 `router/index.ts` 使用 `manifestToRoutes()` 生成路由
3. `AdminLayout.vue` 中的 `routeParentsMap` 硬编码可以随 manifest 重构为 `meta.parentKeys` 字段

**注意：** admin-web 有嵌套路由（`applications/`、`data/`），需要确认 `manifestToRoutes()` 是否支持嵌套 children，或在 `AppManifest` 类型中扩展 `children` 字段。

---

## 三、消除重复配置

### 3.1 提取 UnoCSS 配置到 shared

| 属性 | 值 |
|------|-----|
| **优先级** | P1 |
| **工作量** | S（1h） |

**问题：** `user-web/uno.config.ts`（42 行）和 `admin-web/uno.config.ts`（45 行）完全相同 —— 相同的 presets、colors、boxShadow、borderRadius、shortcuts。修改主题时需改两处。

**建议：**
1. 在 `packages/shared/uno.config.ts` 中创建共享配置：

```ts
// packages/shared/uno.config.ts
import { defineConfig, presetUno, presetAttributify, presetIcons } from 'unocss'

export const sharedUnoConfig = defineConfig({
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
      content: 'var(--color-content-bg)',
      card: 'var(--color-card-bg)',
      text: { primary: 'var(--color-text-primary)', secondary: 'var(--color-text-secondary)', tertiary: 'var(--color-text-tertiary)' },
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

2. 两个 app 的 `uno.config.ts` 改为：

```ts
import { sharedUnoConfig } from '@shared/uno.config'
export default sharedUnoConfig
```

3. 确认 Vite alias 能正确解析 `.ts` 配置文件。如果 UnoCSS 不支持跨包引用配置，可改用 `uno.config.ts` 从 shared 导入 theme 对象合并的方式。

---

### 3.2 提取 createWebRequest() 到 shared

| 属性 | 值 |
|------|-----|
| **优先级** | P1 |
| **工作量** | S（1-2h） |

**问题：** `user-web/src/api/request.ts`（36 行）和 `admin-web/src/api/request.ts`（34 行）几乎完全相同，唯一差异是 `baseURL` 和 `tokenKey` 常量。同时都包含未被调用的 `delay()` 和 `randomDelay()` 死代码。

**建议：**

1. 在 `packages/shared/src/core/http/createWebRequest.ts` 中创建 web 端专用工厂：

```ts
import nprogress from 'nprogress'
import 'nprogress/nprogress.css'
import { createRequest } from './createRequest'
import type { CreateRequestOptions } from './types'

export function createWebRequest(options: CreateRequestOptions) {
  const instance = createRequest(options)

  instance.interceptors.request.use((config) => {
    nprogress.start()
    return config
  })

  instance.interceptors.response.use(
    (resp) => { nprogress.done(); return resp },
    (error) => {
      nprogress.done()
      if (error.response?.status !== 401) {
        const msg = error.response?.data?.error?.message || error.message || '请求失败'
        // message.error 由各端自行注入（避免 core 层依赖 ant-design-vue）
      }
      return Promise.reject(error)
    },
  )

  return instance
}
```

2. 两个 app 的 `request.ts` 简化为：

```ts
// user-web/src/api/request.ts
import { createWebRequest } from '@shared/core/http/createWebRequest'
import { API_BASE_URL, STORAGE_TOKEN_KEY } from '@/utils/constants'

const instance = createWebRequest({
  baseURL: API_BASE_URL,
  tokenKey: STORAGE_TOKEN_KEY,
  onUnauthorized: () => localStorage.removeItem(STORAGE_TOKEN_KEY),
})

export default instance
```

这样同时解决了重复代码 + 死代码清理 + 全局错误 toast 三个问题（一并覆盖任务 1.4 和 5）。

---

## 四、规范化错误处理

### 4.1 AdminLayout 空 catch 块

| 属性 | 值 |
|------|-----|
| **优先级** | P1 |
| **工作量** | XS（15min） |

**问题：** `admin-web/src/layouts/AdminLayout.vue` 中有两处静默吞异常：

```ts
// 第 263-269 行：获取用户信息失败时静默
try {
  const user = await getProfile()
  appStore.setProfile(user)
} catch {
  // mock fallback   ← 用户看不到任何反馈
}

// 第 271-286 行：获取应用列表失败时使用硬编码兜底
try {
  const apps = await getApplications()
  // ...
} catch {
  appMenuItems.value = [
    { id: '1', name: '标准查询', ... },
    // 硬编码兜底数据
  ]
}
```

**建议：** 至少添加 `message.warning('获取用户信息失败')` 或 `message.warning('应用列表加载失败，使用默认菜单')`。对于 profile 获取失败的情况，可以考虑让 `useAppStore` 内置 loading/error 状态，由 UI 层按需消费。

---

### 4.2 useAppStore (user-web) 缺少异常处理

| 属性 | 值 |
|------|-----|
| **优先级** | P2 |
| **工作量** | S（1h） |

**问题：** `user-web/src/stores/app.ts` 中的 `fetchApps()`、`fetchTasks()` 等异步操作没有 try/catch，没有 loading/error 状态。Store 调用方无法知道数据是否在加载中或加载失败。

**建议：** 在 store 中为关键异步操作增加统一的 `loading` / `error` 状态模式。这不是紧急问题（当前 mock 数据不会失败），但当接入真实 API 后，缺少错误状态会导致用户看到空白页面而不知道原因。

---

### 4.3 添加全局 unhandledrejection 监听

| 属性 | 值 |
|------|-----|
| **优先级** | P2 |
| **工作量** | XS（10min） |

**建议：** 在两个 app 的 `main.ts` 中添加兜底：

```ts
window.addEventListener('unhandledrejection', (event) => {
  console.error('Unhandled promise rejection:', event.reason)
  message.error('系统异常，请刷新页面重试')
})
```

此为最低成本的兜底防线 —— 确保未被 catch 的 Promise 异常至少不会完全静默。

---

## 五、清理死代码 + 类型修正

### 5.1 删除未使用的 delay() / randomDelay()

| 属性 | 值 |
|------|-----|
| **优先级** | P2 |
| **工作量** | XS（5min） |

**问题：** 两个 `request.ts` 均导出了 `delay()` 和 `randomDelay()`，但全局搜索无任何调用方。这是开发期调试遗留的死代码。

**当前（`user-web/src/api/request.ts:30-35` / `admin-web/src/api/request.ts:28-33`）：**
```ts
export function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}
export function randomDelay(): Promise<void> {
  return delay(200 + Math.floor(Math.random() * 200))
}
```

**建议：** 删除。如果以后需要模拟网络延迟，应由 mock adapter 的 `delayResponse` 参数统一控制。

---

### 5.2 类型安全修正（3 处 any / 不安全断言）

| 属性 | 值 |
|------|-----|
| **优先级** | P2 |
| **工作量** | S（30min） |

**① admin-web `src/views/api/index.vue` 第 278-279 行：**
```ts
// 当前
sorter: (a: any, b: any) => a.calls - b.calls,

// 建议
sorter: (a: UserUsageRecord, b: UserUsageRecord) => a.calls - b.calls,
```
需在 `packages/shared/src/core/types/apikey.ts` 中补充 `UserUsageRecord` 类型。

**② `user-web/src/views/dubbing/components/DubbingPlayer.vue` 第 98 行：**
```ts
// 当前
function onSpeedChange(e: any): void {

// 建议
function onSpeedChange(value: number): void {
```
Ant Design Vue 的 `<a-slider>` `@change` 事件直接传递数值，不是 Event 对象。

**③ `user-web/src/api/modules/dubbing.ts` 第 17、27、40 行：**
```ts
// 当前
const res = (await ttsClient.get<VoiceItem[]>(...)) as unknown as AxiosResponse<VoiceItem[]>

// 建议：为 ttsClient 单独做 AxiosInstance 类型增强
```
`ttsClient` 是独立的 `axios.create()` 实例，没有挂载 ABP 拦截器，因此响应结构就是标准 `AxiosResponse<T>`，不需要 `as unknown as` 双断言。可以直接用 `axios.get<T>()` 或用泛型约束解决。

---

## 附录 A：已确认良好的模式（保持）

以下模式已经在项目中良好运行，新模块开发时应遵循，无需改动：

| 模式 | 位置 | 说明 |
|------|------|------|
| `AppManifest` 路由声明 | `user-web/src/router/manifests.ts` | 声明式路由，一个条目定义 route/name/title/icon/component |
| URL 契约集中管理 | `packages/shared/src/core/api/urls.ts` | 所有 API 路径一处声明、`as const` 保证字面量类型 |
| API 模块薄封装 | `user-web/src/api/modules/*.ts` | 每个模块只是 `request.get<T>(urls.xxx)`，不含业务逻辑 |
| Mock 模块化注册 | `user-web/src/mock/routes/*.ts` | 每个 `registerXxxMock` 函数独立，支持按模块开关 |
| 共享组件薄封装 | `packages/shared/src/web/components/` | 8 个通用组件，app 特有组件保留在 app 内 |
| 类型按领域拆分 | `packages/shared/src/core/types/` | 15 个类型文件，避免巨石文件 |

## 附录 B：速查清单（按优先级排序）

| # | 任务 | 优先级 | 工作量 | 关联章节 |
|---|------|--------|--------|----------|
| 1 | 移除 API 页面 inline mock 数据 | P1 | S | 1.1 |
| 2 | 清理 mock/index.ts console.log | P1 | XS | 1.2 |
| 3 | request.ts 添加全局错误 toast | P1 | S | 1.4 |
| 4 | 提取 createWebRequest() 到 shared | P1 | S | 3.2 |
| 5 | 提取 UnoCSS 共享配置 | P1 | S | 3.1 |
| 6 | AdminLayout 空 catch 加用户反馈 | P1 | XS | 4.1 |
| 7 | admin-web 路由统一为 manifest | P1 | M | 二 |
| 8 | 删除 delay()/randomDelay() 死代码 | P2 | XS | 5.1 |
| 9 | 类型安全修正（3 处 any） | P2 | S | 5.2 |
| 10 | USE_MOCK 增加环境变量控制 | P2 | XS | 1.3 |
| 11 | useAppStore 增加 loading/error 状态 | P2 | S | 4.2 |
| 12 | 添加全局 unhandledrejection 监听 | P2 | XS | 4.3 |

**建议执行顺序：** #3 + #4 可以合并做（在 shared 中创建 createWebRequest 时一起加 error toast），#1 + #2 可并行，其余独立执行。
