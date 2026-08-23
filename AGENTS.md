# 项目开发规范

## 1. 系统架构

### 1.1 TypeScript
- 严格模式（`tsconfig.base.json`），禁止未使用的变量和参数。
- Vue 组件统一使用 `<script setup lang="ts">`。

### 1.2 路径别名
- `@/` → `src/`
- `@shared/` → `packages/shared/src/`
- 跨包引用统一通过 `@shared/`，禁止相对路径 `../../packages/...`。

### 1.3 样式体系
- 预处理器：**LESS**，全局变量位于 `@shared/web/styles/variables.less`。
- 主题色/间距/阴影/字号等必须引用 LESS 变量（如 `@brand-primary`、`@spacing-md`），禁止硬编码色值或像素。
- 组件库：**ant-design-vue**，图标使用 **@ant-design/icons-vue**。

### 1.4 Dark / Light 主题系统
主题集中管理于 `packages/shared/src/web/styles/themes.less`，所有组件通过 CSS 变量引用，禁止自行定义主题色。

- **切换机制**：`useThemeStore`（Pinia store）设置 `<html data-theme="light|dark">`，持久化到 localStorage（key: `DREDGE_AI_THEME`），支持 `light` / `dark` / `auto`（跟随系统）。
- **Light 模式**（`:root` / `[data-theme="light"]`）：暖白纸色调 — chrome bg `#FCFAF8`（sider + breadcrumb bar），content bg `#F6F3EF`，card bg `#FFFFFF`，text primary `#1C1917`，品牌色 `#0EA5E9`。
- **Dark 模式**（`[data-theme="dark"]`）：深蓝黑玻调 — chrome bg `#0B1220`（sider + breadcrumb bar），content bg `#0B1220`，card bg `#141C2C`，text primary `#E2E8F0`，品牌色 `#60A5FA`。
- **布局**：admin-web 无右侧顶部 header 和面包屑；sider 底部有收起侧栏和退出登录按钮。主题切换按钮（灯泡）位于 sider 品牌行 Logo 右侧。
- **引用方式**：组件中始终使用 CSS 变量或映射的 Less 变量，如 `background: @content-bg` / `color: @text-primary`。**禁止硬编码** `#0EA5E9` 等色值。
- **antd 主题桥接**：`useTheme` composable 将 CSS 变量映射到 ant-design-vue 的 `ConfigProvider` theme tokens，所有 antd 组件自动跟随主题。

---

## 2. 前端规范

### 2.0 新模块开发清单

开发一个新业务模块（如 AI 投标）时，严格按以下顺序执行：

```
1. 定义类型    → packages/shared/src/core/types/<module>.ts
2. 声明 URL    → packages/shared/src/core/api/urls.ts 新增 key
3. 准备 mock   → packages/shared/src/mock/data/<module>.ts
4. 创建 API    → src/api/modules/<module>.ts（导出纯函数，返回 Promise<T>）
                 禁止在组件中直接 import request，必须通过 API 模块封装
5. 创建 mock   → src/mock/routes/<module>.ts（export registerXxxMock(mock, wrap)）
6. 注册 mock   → src/utils/constants.ts 的 MOCK_MODULES 新增 key
                 + src/mock/index.ts 的 modules 数组新增条目
7. 添加路由    → 两端均在 router/manifests.ts 新增 manifest（admin-web 亦已 manifest 化，index.ts 仅做 manifestToRoutes）
8. 创建页面    → src/views/<module>/index.vue（+ components/ 子目录如需）
9. 验证        → pnpm run typecheck
```

> 每一步做完后运行 `pnpm run typecheck`，确保不积累类型错误。
> 所有 API 调用必须经过 `src/api/modules/<module>.ts`，组件禁止直接 `import request`。

---

### 2.1 页面布局 & 间距

- 所有组件（弹框、卡片、表单、页面区块等）采用紧凑间距，**不要依赖组件库默认值**（如 antd 的 `margin-bottom: 24px`）。
- 参考页面：admin-web `views/data/static/standards/index.vue`（列表页标准样板：PageHeader 间距 + 筛选栏 + 表格）、`views/api/index.vue`、`views/dubbing/*`。
- **弹框、按钮、表单间距等 → 加载 `skill: layout-conventions`**

#### 间距速查表

| 场景 | 间距 | LESS 变量 |
|------|------|-----------|
| 页面外边距 | 24px | `@page-padding` |
| 同级卡片/区块之间 | 24px | `@spacing-xl` |
| PageHeader 与下方内容 | 12px | `@spacing-md` |
| 卡片 header 与 body（flush 模式） | 用 10px 内边距替代 antd 默认 24px | — |
| 表格上方 filter 栏与表格 | 16px | `@spacing-base` |
| 弹框 body 内间距 | 16~20px | `@spacing-base` ~ `@spacing-lg` |
| 弹框 footer 按钮间距 | 8px | `@spacing-sm` |
| MetricCard 行与图表之间 | 24px | `@spacing-xl` |

#### 配色速查表

| 场景 | 应使用的颜色 | 标记方式 |
|------|-------------|----------|
| 成功/已完成 | green | `<a-tag color="green">` |
| 进行中/处理中 | blue | `<a-tag color="blue">` |
| 失败/错误 | red | `<a-tag color="red">` |
| 高风险 | `#EF4444` | `<a-tag color="#EF4444">` |
| 中风险 | `#F59E0B` | `<a-tag color="#F59E0B">` |
| 低风险 | `#3B82F6` | `<a-tag color="#3B82F6">` |
| KPI 卡片色 | `@brand-primary` / `@accent` / `@success` / `@warning` | `MetricCard` 的 `color` prop |
| 男声标识 | `#2563EB` | css class |
| 女声标识 | `#DB2777` | css class |
| 童声标识 | `#D97706` | css class |

#### 颜色规范

| 规则 | 说明 |
|------|------|
| 裸 hex 禁用 | `#FF4D4F` → `@danger`、`#10B981` → `@success`、`#8B5CF6` → `@accent` |
| color-mix 替代旧函数 | 用 `color-mix(in srgb, @brand-primary 8%, transparent)` 代替 `fade()`/`darken()`/`lighten()` |
| 语义色须定义变量 | 性别/风险标识等须在 `variables.less` 定义专属变量后再引用（如 `@voice-gender-male`） |

### 2.2 表格

- **全局居中**：已在 `global.less` 设置 thead + tbody `text-align: center`，无需写 `align="center"`。
- **列宽**：固定字段（序号、日期、状态、操作）设 `width: Npx`，文本字段（名称、描述）不设 `width` 自适应。
- **操作列**：固定 `width: 180px`，`white-space: nowrap`，按钮用 `a-button type="link"` 紧凑排列。
- **分页**：统一 `pageSize: 15`（管理端列表）或 `pageSize: 10`（弹框内表格），用 `showTotal` 展示总数。
- **loading**：表格数据加载中时传入 `:loading="loading"`，避免白屏。
- **empty**：无数据时可通过 `:locale="{ emptyText: '暂无数据' }"` 自定义空态文案。
- **row-key**：必须设置 `row-key="id"`，避免 antd 警告。
- **响应式**：多列表格加 `:scroll="{ x: 1100 }"` 防止窄屏溢出。
- **标准样板**：管理端列表页统一使用共享组件 `@shared/web` 的 `DataTable`（内置 `size="small"`、`pageSize: 15`、`showTotal`、`row-key`、操作列 `fixed='right'`、列宽可拖拽、横向自适应、配置驱动筛选栏），**禁止在页面内自行实现 a-table + 列宽拖拽/筛选栏/自适应逻辑**；参考 admin-web `views/data/static/standards/index.vue` 与 `views/applications/control.vue` 的用法。
- **模板**：

```ts
const columns = [
  { title: '序号', dataIndex: 'index', width: 80 },
  { title: '名称', dataIndex: 'name' }, // 自适应
  { title: '类型', dataIndex: 'type', width: 120 },
  { title: '状态', dataIndex: 'status', width: 100 },
  { title: '创建时间', dataIndex: 'createdAt', width: 180 },
  { title: '操作', dataIndex: 'action', width: 180 },
]
```

#### 表格上方筛选栏（Filter Bar）

- 筛选栏**优先**放在 `SectionCard` 外面、表格卡片上方；若受结构限制仍在卡片内，也必须去掉内边距和边框，避免出现独立背景工具栏。
- 筛选栏本身**不加背景、不加边框**，只使用 flex 行 + 8px 间距 + 下方 16px 间距。
- 样式参考 `admin-web/src/views/data/static/standards/index.vue` 的 `.standards-filter-bar`：

```less
.standards-filter-bar {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  flex-wrap: wrap;
  margin-bottom: @spacing-base;
}
```

- 筛选控件使用默认尺寸（不写 `size`），搜索框/选择器宽度建议 100~240px，按实际字段数调整。
- 若某页有“重置”按钮，应紧跟最后一个筛选字段，不要用 `margin-left: auto` 推到右侧。
- 新页面的筛选栏一律照此实现，不新增背景、边框或自定义样式变体。



### 2.3 动效
- 出场动画默认 0.6s，必须配合缓动曲线（`power2.out` 或 `cubic-bezier`），禁止线性淡入。
- 多元素出现时使用交错延迟（Stagger），禁止同时僵硬弹现。
- 动画优先使用 `transform` 和 `opacity`，禁止引发重排的属性动画，确保 60fps。
- **需要动效时，按需加载对应 skill：**
  - GSAP 通用动画 → `gsap`
  - 产品级动效系统（Stripe/Linear 风格） → `animation-systems`
  - IntersectionObserver 滚动触发 → `animation-on-scroll`
  - GSAP + Lenis 电影级动画 → `cinematic-gsap-lenis-motion-system`
  - 滚动叙事（粘性卡片、渐入转场） → `cinematic-scroll-storytelling`
  - ScrollTrigger 粘性产品叙事 → `gsap-scrolltrigger-storytelling`
  - 逐词遮罩揭示 → `masked-reveal`
  - 逐词淡入上升 → `staggered-word-reveal`
  - 无缝跑马灯 → `marquee-loop`
  - SVG 粘性流体 → `gooey-blob-system`
  - 光标跟随聚光灯 → `reveal-hover-effect`

### 2.4 3D / WebGL
- **涉及 3D 场景、粒子、地球、物理引擎、WebGL 背景时，按需加载对应 skill：**
  - Three.js 3D 场景 → `threejs`
  - 3D 几何物体（PBR 材质、旋转浮动） → `webgl-3d-object`
  - 全屏激光效果 → `webgl-laser`
  - WebGL 落地页方向引导 → `webgl-landing-steering`
  - 透视网格背景 → `background-grid-webgl`
  - globe.gl 3D 地球数据可视化 → `globe-gl`
  - 3D 粒子星球 → `globe-particles`
  - 轻量交互式地球 → `cobejs`
  - Vanta.js 动态背景 → `vantajs`
  - Unicorn Studio 嵌入 → `unicorn-studio`
  - 2D 物理引擎 → `matterjs`

### 2.5 UI 设计系统
- **涉及 UI 整体风格重设时，选择并加载对应 skill：**
  - 极简 Agency → `agency-grid-layout-minimal`
  - 蓝色云朵清洁风 → `blue-cloudy-clean-modern`
  - 深色激光玻璃 → `blue-laser-clean-glass-layout`
  - 典藏书籍排版 → `book-serif-index`
  - 亮绿色技术 → `bright-green-tech-system-webgl`
  - 米色极简浅色 → `clean-minimal-beige-light-mode`
  - 深蓝高对比 → `dark-blue-contrasting-clean`
  - 深色玻璃 → `dark-glass-clean-layout`
  - 暗色抖动激光 → `dither-laser-dark-mode`
  - 编辑技术融合 → `editorial-tech`
  - 框架深色技术（边框渐变） → `framed-tech-dark-border-gradient`
  - 紫红容器技术 → `funky-purple-container-tech`
  - 玻璃暗色钟表 → `glass-dark-mode-clock`
  - 深色玻璃态 UI → `glass-dark-ui`
  - 高对比拟物 → `high-contrast-skeuomorphic-clean`
  - 图片优先网格 → `image-first-grid-layout`
  - 浅色纸张技术 → `light-mode-paper-technical`
  - 网格渐变深蓝 → `mesh-gradient-dark-blue-clean`
  - 嵌套容器 Agency → `nested-container-clean-agency`
  - 嵌套容器框架 → `nested-container-frames`
  - 橙色纸张 SaaS → `orange-clean-paper-saas`
  - 技术分屏 → `split-layout-technical`
  - 深色技术绿 → `tech-green-dark-mode-modern`
  - 线框技术布局 → `technical-wireframe-info-layout`

### 2.6 布局 & 网格
- **需要布局结构时，按需加载对应 skill：**
  - 极简框架网格（L形角标、斜线纹理） → `framed-grid-layout`
  - 容器引导线 → `container-lines`
  - 对角裁切边角 → `corner-diagonals`
  - 角落激光构图 → `corner-lasers`

### 2.7 CSS 特效
- **需要 CSS 视觉效果时，按需加载对应 skill：**
  - 渐变边框 → `css-border-gradient`
  - CSS 遮罩淡出 → `css-alpha-masking`
  - 多层渐进模糊 → `progressive-blur`
  - 暗色抖动背景 → `dither-background`
  - 精制分层阴影 → `beautiful-shadows`
  - 拟物 UI → `skeuomorphic-ui`
  - 暗色氛围背景 → `atmosphere-background`
  - 装饰数字标记 → `number-details`

### 2.8 图标 & 品牌
- **需要图标时，加载对应 skill：**
  - Solar Duotone Bold 图标 → `solar-duotone-bold`
  - 品牌标志（Iconify Simple Icons） → `company-logos`

### 2.9 框架
- Tailwind CSS → `tailwindcss`

### 2.10 图表 (ECharts)
涉及图表样式（ECharts option、主题适配、柱状图、饼图模板等）时，**必须先读** [chart-conventions.md](./chart-conventions.md) 并按其规范执行。

### 2.11 共享组件速查

以下组件位于 `@shared/web/components/`，跨模块复用，禁止在视图中重复实现等价功能：

| 组件 | 用途 | 关键 Props |
|------|------|-----------|
| `PageHeader` | 页面标题 + 描述 + 右侧操作区 | `title`, `description`, `#extra` slot |
| `SectionCard` | 带标题的卡片容器 | `title`, `flush`(去除 body 顶部默认 24px), `nopad`(完全去除 body padding), `#extra` slot |
| `MetricCard` | KPI 指标卡（网格排列） | `title`, `value`, `suffix`, `icon`(字符串，如 `"SoundOutlined"`), `color`(CSS 色值), `loading` |
| `ChartContainer` | ECharts 容器 + loading 态 | `option`, `height`(默认 300px), `loading` |
| `AppButton` | 统一按钮（语义化 variant + 尺寸） | `variant`(primary/secondary/danger/text/link/dashed), `size`(sm/md/lg), `danger`, `block`, `loading`, `disabled`, `htmlType` |
| `DataSkeleton` | 加载骨架屏 | 包裹内容区域，显示 loading 效果 |
| `ErrorBoundary` | 全局错误兜底 | 包裹 `<router-view>`，已在 App.vue 中使用 |
| `Logo` | 品牌 Logo + 标题 | 已在 Layout 中使用，一般无需手动引用 |
| `ThemeToggle` | 深色/浅色切换按钮 | 已在 Layout 中使用，一般无需手动引用 |

### 2.12 弹框 / 模态框 / 抽屉

- **弹框、表单、确认等 → 加载 `skill: layout-conventions`**

| 场景 | 宽度 |
|------|------|
| 简单表单 | `440px` |
| 详情展示 | `520px` |
| 大表单 | `640px` |
| 大列表 | `800px` |

### 2.13 组件编码模式

以下模式提炼自 AI 配音模块，适用于所有业务模块开发。

#### 状态管理：Props Down, Events Up
- `index.vue` 是**唯一**持有业务状态的组件，子组件只收 props、只 emit 事件，不跨组件共享 state。
- 所有 API 调用集中在 `index.vue` 中，子组件**禁止**直接 `import request` 或调用 API 函数。

```ts
// ✅ index.vue：调用 API，管理所有 ref
// ❌ 子组件中直接调 API
import { getVoices } from '@/api/modules/dubbing'

const voices = ref<VoiceItem[]>([])
onMounted(async () => { voices.value = await getVoices() })
```

#### Props / Emits 类型
```ts
defineProps<{ voices: VoiceItem[], loading?: boolean, disabled?: boolean }>()
defineEmits<{ 'update:modelValue': [value: string], 'select': [id: string] }>()
```

#### 状态三态覆盖
每个数据域必须覆盖 loading / empty / error 三种 UI：

| 状态 | UI 处理 |
|------|---------|
| loading | `<a-skeleton>` 或 `DataSkeleton` |
| loaded（有数据） | 正常渲染 |
| loaded（空） | `<a-empty description="..." />` |
| error | `<a-result status="error">` + `message.error()` |

```vue
<a-skeleton v-if="loading" :paragraph="{ rows: 3 }" />

<a-empty v-else-if="data.length === 0" description="暂无数据" />

<div v-else>
<!-- 正常渲染 -->
</div>
```

#### CSS 命名：BEM 风格
```less
.dubbing-page { }             // Block
.dubbing-page__body { }       // Block__Element
.voice-item--selected { }     // Block--Modifier
.voice-item__gender--male { } // Block__Element--Modifier
```

#### :deep() 覆盖 antd 样式
```less
.section-card {
  :deep(.ant-card-body) { padding: @spacing-md @spacing-xl; }
  :deep(.ant-tabs-tab) { padding: 6px 10px; }
}
```

#### prefers-reduced-motion 降级
所有动效必须提供：
```less
@media (prefers-reduced-motion: reduce) {
  .result-area { animation: none; }
  .fade-enter-active { transition: none; }
}
```

#### 禁止事项
- 禁止子组件直接 import API 模块（音频播放等须即时生成 blob 的特殊场景除外，但需注释说明原因）。
- 禁止 CSS / 模板中混用 tab 和 space 缩进。

### 2.14 UI 零件尺寸 / 样式规范

所有 ant-design-vue 组件统一使用以下尺寸和样式规则，**各页面禁止自行发挥**。

#### 按钮大小

| 场景 | 用法 | 示例 |
|------|------|------|
| 表格行操作按钮 | `<AppButton variant="link" size="sm">` | 编辑、删除、查看 |
| PageHeader `#extra` 次要操作 | `<AppButton size="sm">` | 历史记录、刷新 |
| PageHeader `#extra` 主要操作 | `<AppButton variant="primary">`（默认 md） | 创建、新增 |
| 卡片 `#extra` 操作 | `<AppButton size="sm">` 或 `variant="link" size="sm"` | 制音、下载 |
| 弹框 footer 操作 | `<AppButton>` / `<AppButton variant="primary">`（默认 md） | 取消、确认 |
| 主交互按钮 | `<AppButton variant="primary" size="lg">` | 开始生成 |

- 按钮统一使用共享 `AppButton`（`@shared/web`），禁止直接使用 `a-button`（`AppButton` 内部封装除外）。
- 语义：`primary` 主操作 / `secondary` 次要操作（品牌色描边）/ `danger` 危险 / `text` 文字 / `link` 链接 / `dashed` 虚线。
- 尺寸：`sm`（表格、PageHeader / 卡片 extra）/ `md`（默认，弹框 footer）/ `lg`（主 CTA）。

#### 表格

```vue
<a-table size="small" :pagination="{ pageSize: 15, showTotal: (t) => `共 ${t} 条` }" />
```
- 所有管理列表统一 `size="small"`（紧凑）
- 弹框内表格 `pageSize: 10`，管理端列表 `pageSize: 15`

#### 输入框 / 选择器 / 搜索框

```vue
<a-input size="medium" />          <!-- 默认，不写 size -->

<a-select size="medium" />         <!-- 默认 -->

<a-input-search size="medium" />   <!-- 默认 -->
```
- 不在表单内的搜索框（如 filter 栏）用默认 medium，设 `style="width:240px"`

#### 分段控件 / 单选组 / 开关

```vue
<a-segmented />                             <!-- 默认 middle -->

<a-radio-group size="small" button-style="solid" />

<a-switch size="small" />
```
- `a-segmented` 只用于视图/模式切换，不用 size 属性
- `a-radio-group` 和 `a-switch` 统一 `size="small"`

#### Tabs 样式覆盖

任何使用 `a-tabs` 的页面，必须在 scoped less 中覆盖间距：

```less
:deep(.ant-tabs-nav) { margin-bottom: @spacing-sm; }
:deep(.ant-tabs-tab) { padding: 6px 10px; }
```

#### 日期选择器

```vue
<a-range-picker size="small" />

<a-date-picker size="small" />
```

---

## 3. 后端规范

### 3.1 接口规范（ABP）
涉及接口设计（URL、请求/响应格式、错误处理、DTO 约定等）时，**必须先读** [abp-api-conventions.md](./abp-api-conventions.md) 并按其规范执行。
