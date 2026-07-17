# 双端独立架构重构设计

> 日期：2026-07-17
> 状态：待实现
> 范围：基于 `docs/TECH_STACK.md` 完全重构现有 `platform/` 原型，交付两个独立的前端工程

## 1. 背景与目标

现有 `platform/` 是单 Vite 应用 + 双布局的早期原型，存在以下问题：
- 单仓双布局无法承载两个团队独立迭代
- 技术栈精简，未达到 `TECH_STACK.md` 工程化要求
- 审美偏朴素，缺少完整设计系统
- mock 数据集中在一个文件，未走 axios，难以为后续真实接口对接铺路

本次重构目标：
1. 拆分为 `user-web` 与 `admin-web` 两个完全独立的前端工程，各自端口
2. 全新实现 `user-web`，作为产品评审与后续模块开发基础
3. 为 `admin-web` 搭建新架构脚手架（仅 dashboard 完整示范，其余骨架占位），供 adminWeb 团队接手
4. 引入精简企业档技术栈与完整设计系统，提升审美
5. 扩充 mock 数据，通过 axios + mock-adapter 模拟接口调用
6. 删除旧 `platform/` 目录

## 2. 非目标

- 不接入真实后端
- 不实现 admin-web 的 permissions / applications / data / analytics 业务页面
- 不引入 GIS、实时通信、视频、@surely-vue/table 等重型依赖
- 不做移动端适配（桌面优先）

## 3. 仓库结构

两个独立项目，用 pnpm workspace 编排脚本，但**不共享代码**。

```
d:\AI\DredgeAI\
├── package.json              # 根 workspace
├── pnpm-workspace.yaml
├── tsconfig.base.json
├── .npmrc
├── user-web/                 # 端口 5173，完整实现
│   ├── package.json
│   ├── vite.config.ts
│   ├── tsconfig.json
│   ├── tsconfig.node.json
│   ├── index.html
│   ├── env.d.ts
│   └── src/
│       ├── api/              # axios 实例 + mock-adapter + 业务接口模块
│       │   ├── request.ts
│       │   ├── mock/index.ts
│       │   └── modules/{user,app,bid,standard,profile,apikey}.ts
│       ├── mock/             # 静态 TS mock 数据，按模块拆分
│       │   ├── user.ts
│       │   ├── app.ts
│       │   ├── task.ts
│       │   ├── file.ts
│       │   ├── bid.ts
│       │   ├── standard.ts
│       │   ├── apikey.ts
│       │   ├── notification.ts
│       │   └── chart.ts
│       ├── types/            # 全局 TS 类型
│       ├── utils/            # dayjs / lodash 封装
│       ├── composables/      # useTable / usePagination / useTheme
│       ├── components/       # 业务通用组件
│       │   ├── BaseLayout/
│       │   ├── SectionCard.vue
│       │   ├── MetricCard.vue
│       │   ├── StatusTag.vue
│       │   ├── PageHeader.vue
│       │   ├── SearchInput.vue
│       │   ├── ChartContainer.vue
│       │   ├── DataSkeleton.vue
│       │   └── EmptyState.vue
│       ├── layouts/
│       │   └── UserLayout.vue
│       ├── router/
│       │   └── index.ts
│       ├── stores/           # Pinia + persistedstate
│       │   ├── user.ts
│       │   └── app.ts
│       ├── styles/
│       │   ├── variables.less
│       │   ├── global.less
│       │   └── reset.less
│       ├── uno.config.ts
│       ├── App.vue
│       └── main.ts
├── admin-web/                # 端口 5174，架构脚手架
│   ├── package.json
│   ├── vite.config.ts
│   ├── tsconfig.json
│   ├── tsconfig.node.json
│   ├── index.html
│   ├── env.d.ts
│   └── src/
│       ├── api/              # 完整 axios + mock-adapter 基础设施
│       │   ├── request.ts
│       │   ├── mock/index.ts
│       │   └── modules/{dashboard,permission,application,data,analytics}.ts
│       ├── mock/             # 仅 dashboard 所需示例 mock
│       │   ├── dashboard.ts
│       │   └── chart.ts
│       ├── types/
│       ├── utils/
│       ├── composables/
│       ├── components/       # 与 user-web 同套设计 token 的组件
│       │   ├── BaseLayout/
│       │   ├── SectionCard.vue
│       │   ├── MetricCard.vue
│       │   ├── PageSkeleton.vue   # 骨架占位组件
│       │   ├── PageHeader.vue
│       │   ├── ChartContainer.vue
│       │   └── DataSkeleton.vue
│       ├── layouts/
│       │   └── AdminLayout.vue
│       ├── router/
│       │   └── index.ts
│       ├── stores/
│       │   └── app.ts
│       ├── styles/
│       │   ├── variables.less
│       │   ├── global.less
│       │   └── reset.less
│       ├── uno.config.ts
│       ├── App.vue
│       └── main.ts
└── docs/
    └── superpowers/specs/2026-07-17-dual-web-refactor-design.md
```

## 4. 技术栈

两端一致，精简企业档：

| 类别 | 技术 | 版本 |
|---|---|---|
| 核心 | vue | ^3.5.13 |
| 类型 | typescript | ~5.7.3 |
| 构建 | vite | ^6.3.1 |
| 类型检查 | vue-tsc | ^2.2.8 |
| 状态 | pinia | ^2.2.8 |
| 持久化 | pinia-plugin-persistedstate | ^4.2.0 |
| 路由 | vue-router | ^4.5.0 |
| UI | ant-design-vue | ^4.2.6 |
| 图标 | @ant-design/icons-vue | ^7.0.1 |
| 样式预处理 | less | ^4.2.2 |
| 原子化 CSS | unocss | ^66.1.0 |
| HTTP | axios | ^1.7.9 |
| Mock 拦截 | axios-mock-adapter | ^2.1.0 |
| 可视化 | echarts / vue-echarts | ^5.6.0 / ^7.0.3 |
| 工具 | dayjs | ^1.11.13 |
| 工具 | lodash-es | ^4.17.21 |
| 工具 | @vueuse/core | ^11.3.0 |
| 进度条 | nprogress | ^0.2.0 |
| 并发脚本 | concurrently | ^9.1.0 |
| Vue 插件 | @vitejs/plugin-vue | ^5.2.3 |

> axios 升级到 1.x 以避免 0.27 已知问题；@vueuse/core 用 11.x 适配 Vue 3.5；vue-echarts 用 7.x 适配 echarts 5。

## 5. 设计系统（现代企业级）

参考 Linear / Vercel Dashboard / Stripe 的视觉调性。

### 5.1 色彩

| Token | 值 | 用途 |
|---|---|---|
| `@brand-primary` | `#0EA5E9` (sky-500) | 主色，按钮、链接、强调 |
| `@brand-primary-hover` | `#0284C7` (sky-600) | 主色 hover |
| `@brand-gradient` | `linear-gradient(135deg, #0EA5E9 0%, #06B6D4 100%)` | 品牌 hero / 头部背景 |
| `@accent` | `#06B6D4` (cyan-500) | 强调点缀（保留原青色基因） |
| `@success` | `#10B981` (emerald-500) | 成功状态 |
| `@warning` | `#F59E0B` (amber-500) | 警告状态 |
| `@danger` | `#EF4444` (rose-500) | 危险状态 |
| `@info` | `#3B82F6` (blue-500) | 信息状态 |
| `@sidebar-bg` | `#0F172A` (slate-900) | 侧边栏背景 |
| `@sidebar-bg-2` | `#1E293B` (slate-800) | 侧边栏次级 |
| `@content-bg` | `#F8FAFC` (slate-50) | 内容区背景 |
| `@card-bg` | `#FFFFFF` | 卡片背景 |
| `@text-primary` | `#0F172A` (slate-900) | 主文字 |
| `@text-secondary` | `#475569` (slate-600) | 次文字 |
| `@text-tertiary` | `#94A3B8` (slate-400) | 三级文字 / 占位 |
| `@border-color` | `#E2E8F0` (slate-200) | 边框 |
| `@divider-color` | `#F1F5F9` (slate-100) | 分隔线 |

### 5.2 字号与字重

| Token | 值 |
|---|---|
| `@font-size-xs` | 12px |
| `@font-size-sm` | 13px |
| `@font-size-base` | 14px |
| `@font-size-lg` | 16px |
| `@font-size-xl` | 18px |
| `@font-size-2xl` | 22px |
| `@font-size-3xl` | 28px |
| `@font-size-4xl` | 36px |
| `@font-weight-regular` | 400 |
| `@font-weight-medium` | 500 |
| `@font-weight-semibold` | 600 |
| `@font-weight-bold` | 700 |
| `@font-family` | `-apple-system, "PingFang SC", "Microsoft YaHei", "Segoe UI", sans-serif` |

### 5.3 圆角与间距

| Token | 值 |
|---|---|
| `@radius-sm` | 6px |
| `@radius-base` | 8px |
| `@radius-lg` | 12px |
| `@radius-xl` | 16px |
| `@spacing-xs` | 4px |
| `@spacing-sm` | 8px |
| `@spacing-md` | 12px |
| `@spacing-base` | 16px |
| `@spacing-lg` | 20px |
| `@spacing-xl` | 24px |
| `@spacing-2xl` | 32px |
| `@spacing-3xl` | 40px |
| `@spacing-4xl` | 48px |

### 5.4 阴影

| Token | 值 |
|---|---|
| `@shadow-sm` | `0 1px 2px rgb(0 0 0 / 0.05)` |
| `@shadow-md` | `0 4px 12px rgb(15 23 42 / 0.08)` |
| `@shadow-lg` | `0 12px 32px rgb(15 23 42 / 0.12)` |
| `@shadow-brand` | `0 8px 24px rgb(14 165 233 / 0.25)` |

### 5.5 布局尺寸

| Token | 值 |
|---|---|
| `@sidebar-width` | 240px |
| `@sidebar-collapsed-width` | 64px |
| `@header-height` | 64px |
| `@content-max-width` | 1440px |
| `@page-padding` | 24px |

### 5.6 动效

| Token | 值 |
|---|---|
| `@transition-fast` | 150ms ease |
| `@transition-base` | 200ms ease |
| `@transition-slow` | 300ms ease |
| hover lift | `transform: translateY(-2px); box-shadow: @shadow-md` |
| route enter | `opacity: 0 → 1 + translateY(4px → 0)` 200ms |
| skeleton | shimmer 1.4s infinite |

### 5.7 UnoCSS 主题映射

UnoCSS preset 配置中将上述 token 映射为原子类，例如 `text-primary`、`bg-card`、`shadow-brand`、`rounded-lg`。

## 6. 路由设计

### 6.1 user-web (端口 5373)

| 路径 | 名称 | 视图 |
|---|---|---|
| `/` | - | redirect → `/dashboard` |
| `/dashboard` | UserDashboard | 工作台首页 |
| `/apps` | UserApps | 应用广场 |
| `/bid-review` | BidReview | AI 审标 |
| `/standards` | Standards | 标准查询 |
| `/profile` | Profile | 个人中心 |
| `/api` | ApiManage | API 管理 |

所有路由懒加载。根路径 `/` 用 UserLayout 包裹。

### 6.2 admin-web (端口 5374)

| 路径 | 名称 | 视图 |
|---|---|---|
| `/` | - | redirect → `/dashboard` |
| `/dashboard` | AdminDashboard | 管理工作台（完整实现） |
| `/permissions` | Permissions | 骨架占位 |
| `/applications` | Applications | 骨架占位 |
| `/data` | DataGovernance | 骨架占位 |
| `/analytics` | Analytics | 骨架占位 |

根路径 `/` 用 AdminLayout 包裹。

## 7. Mock 数据策略

### 7.1 拦截机制

- `api/request.ts` 创建 axios 实例，baseURL `/api`，统一响应结构 `{ code: 0, data: T, message: string }`
- `api/mock/index.ts` 创建 `MockAdapter` 实例，注册各模块路由
- 模拟网络延迟 200-400ms 随机
- 业务接口模块（`api/modules/*.ts`）封装 `Promise<T>` 调用，对页面透明
- mock 开关由 `api/mock/index.ts` 中的 `USE_MOCK` 常量控制（默认 `true`），未来切真实接口只需改为 `false`，不依赖 `.env` 文件（已被 .gitignore 忽略）

### 7.2 user-web Mock 模块

| 模块 | 文件 | 内容 |
|---|---|---|
| 用户 | `mock/user.ts` | 当前用户信息、授权范围、偏好 |
| 应用 | `mock/app.ts` | 8 个应用卡片 + 4 类场景分类 + 收藏列表 |
| 任务 | `mock/task.ts` | 6 条最近任务 + 状态分布 |
| 文件 | `mock/file.ts` | 6 条最近文件 |
| 审标 | `mock/bid.ts` | 4 步骤 + 5 条风险 + 5 条会话历史 + 详情 |
| 标准 | `mock/standard.ts` | 4 条查询历史 + 5 条命中结果 + 标准分类树 + 推荐问题 |
| API Key | `mock/apikey.ts` | 4 条 Key + 模型类型 + 按模型/Key 用量统计 |
| 通知 | `mock/notification.ts` | 8 条通知（系统/业务/审计三类） |
| 图表 | `mock/chart.ts` | dashboard 折线/柱状/饼图数据 + 个人效率趋势 |

### 7.3 admin-web Mock 模块

| 模块 | 文件 | 内容 |
|---|---|---|
| 工作台 | `mock/dashboard.ts` | 4 个指标 + 6 条待办告警 + 5 条应用排行 + 5 条审核提醒 |
| 图表 | `mock/chart.ts` | 调用趋势折线 + 应用排行柱状 + 模型成本饼图 |

## 8. 页面详细设计

### 8.1 user-web

#### Dashboard 工作台
- 顶部欢迎区：用户名 + 岗位 + 部门标签 + 快捷搜索框 + 3 个快捷任务按钮
- 主区双栏：
  - 左 8/12：推荐任务卡片网格（2×2）、最近任务列表（含状态 tag、续做按钮）
  - 右 4/12：授权应用竖列、最近文件列表（含文件类型图标）、个人效率 mini 折线图

#### Apps 应用广场
- 顶部场景筛选 segmented（全部 / 日常办公 / 专业业务 / 知识查询 / 开发接口）
- 搜索框
- 应用卡片网格（3 列），卡片含图标、标题、描述、场景标签、收藏按钮、进入按钮
- 待申请状态显示「申请权限」按钮

#### BidReview AI 审标
- 左栏（6/16）：a-steps 垂直步骤流（上传 → 识别 → 风险 → 报告）+ 历史会话列表
- 中栏（6/16）：文档预览区（上传组件 + 文档内容片段高亮）+ 追问输入框
- 右栏（4/16）：风险面板（按等级分组，含来源章节、原文引用）+ 操作按钮（导出报告）

#### Standards 标准查询
- 顶部查询栏（关键词输入 + 标准编号 + 自然语言切换）
- 主区双栏：
  - 左 10/16：查询结果卡片（标准号、标题、命中条款、引用来源、展开原文）
  - 右 6/16：推荐问题列表 + 查询历史列表

#### Profile 个人中心
- 左栏：用户头像卡（姓名、岗位、部门、授权范围 tag）+ 偏好设置表单
- 右栏：a-descriptions 个人信息 + 最近活动时间轴

#### Api API 管理
- 顶部：API Key 列表表格（名称、Key 脱敏、模型、状态、用量、操作）
- 弹窗：创建 Key 表单
- 右侧摘要：按模型用量饼图 + 按 Key 用量柱状图 + 调用说明卡片

### 8.2 admin-web

#### Dashboard 管理工作台（完整实现）
- 顶部：4 个指标卡片（调用量 / 活跃用户 / 异常告警 / 待审核），含趋势 tag 和 mini sparkline
- 主区双栏：
  - 左 8/12：调用趋势折线图（echarts）+ 待办告警列表（含严重程度 tag、处理按钮）
  - 右 4/12：应用排行 TOP5 列表 + 审核提醒列表

#### 其余页面骨架
统一使用 `PageSkeleton.vue`：
```vue
<template>
  <PageHeader :title="title" :description="description" />
  <a-alert type="info" show-icon
    message="本页由 adminWeb 团队基于此架构实现，参考 dashboard 页与 design token 体系" />
  <SectionCard title="示意结构">
    <a-empty description="待 adminWeb 团队实现" />
  </SectionCard>
</template>
```
每个骨架页文件顶部 JSDoc 注释标明该页应实现的功能模块。

## 9. 组件设计

### 共享组件清单（两端各自维护一份，但视觉一致）

| 组件 | 职责 |
|---|---|
| `BaseLayout` | 接收 sidebar 菜单配置、header 右侧 slot、内容区 slot |
| `SectionCard` | 带标题、副标题、操作区 slot 的卡片容器 |
| `MetricCard` | 指标卡（标题、数值、趋势、mini sparkline） |
| `StatusTag` | 统一状态标签（颜色映射） |
| `PageHeader` | 页面标题 + 描述 + 右侧操作区 |
| `SearchInput` | 带搜索图标的输入框 |
| `ChartContainer` | echarts 容器（自适应、loading） |
| `DataSkeleton` | 数据加载骨架屏 |
| `EmptyState` | 空状态 |
| `PageSkeleton`（仅 admin-web） | 骨架占位页 |

## 10. 工程化约定

- TypeScript 严格模式，禁用 `any`
- 组件用 SFC + `<script setup>`，单组件 ≤200 行
- `@/` 别名指向各自 `src/`
- 路由懒加载，name 与菜单 API 响应匹配（为未来动态路由铺路）
- 认证令牌 key：`STORAGE_TOKEN_KEY`，存于 localStorage
- ESLint + Prettier + Stylelint 配置在各包内（避免跨包依赖）
- 不使用 `.env` 文件（已被 .gitignore 忽略），环境差异通过 `vite.config.ts` 的 `mode` 和代码常量处理

## 11. 根目录脚本

```json
{
  "name": "dredge-ai-workspace",
  "private": true,
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
  }
}
```

`pnpm-workspace.yaml`：
```yaml
packages:
  - 'user-web'
  - 'admin-web'
```

## 12. 端口与跨端切换

- user-web dev server: `5373`
- admin-web dev server: `5374`
- 跨端切换通过 `window.location.href` 跳转，不共享状态

## 13. 迁移与清理

- 重构完成后删除 `platform/` 目录
- `docs/` 保留并继续作为文档目录
- `.gitignore` 更新加入新结构相关条目

## 14. 验收标准

1. `pnpm install` 在根目录成功安装两端依赖
2. `pnpm dev` 同时启动 5373 与 5374 两个端口
3. user-web 6 个页面均可访问，视觉符合设计系统，mock 数据加载正常
4. admin-web dashboard 完整可访问，4 个骨架页正确显示占位
5. `pnpm typecheck` 通过
6. `pnpm build` 双端均构建成功
7. 旧 `platform/` 目录已删除
