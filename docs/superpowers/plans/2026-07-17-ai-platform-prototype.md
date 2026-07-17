# AI Platform Prototype Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a runnable mid-fidelity prototype for `userWeb` and `adminWeb` focused on IA, navigation, and key page layouts without backend integration.

**Architecture:** Use a single Vite + Vue 3 + TypeScript app with Vue Router to host two prototype areas: `userWeb` and `adminWeb`. Each prototype page is implemented as a presentational view with shared layout components and static mock data, keeping the structure ready for later real integration.

**Tech Stack:** Vue 3, TypeScript, Vite, Vue Router 4, Pinia, Ant Design Vue 4, Less

---

### Task 1: Scaffold Prototype App

**Files:**
- Create: `package.json`
- Create: `src/main.ts`
- Create: `src/App.vue`
- Create: `src/router/index.ts`
- Create: `src/style.less`

- [ ] **Step 1: Initialize the Vite Vue TypeScript app and install dependencies**

Run: `pnpm create vite@latest . --template vue-ts`
Run: `pnpm add vue-router pinia ant-design-vue @ant-design/icons-vue less`
Expected: project files created and install completes successfully

- [ ] **Step 2: Wire the app entry with router, store, and Ant Design Vue**

```ts
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import Antd from 'ant-design-vue'
import App from './App.vue'
import router from './router'
import './style.less'
import 'ant-design-vue/dist/reset.css'

createApp(App).use(createPinia()).use(router).use(Antd).mount('#app')
```

- [ ] **Step 3: Verify the starter app runs**

Run: `pnpm run dev --host 0.0.0.0 --port 4173`
Expected: Vite dev server starts without TypeScript errors

### Task 2: Build Shared Prototype Shell

**Files:**
- Create: `src/layouts/PrototypeLayout.vue`
- Create: `src/components/AppHeader.vue`
- Create: `src/components/AppSider.vue`
- Create: `src/components/SectionCard.vue`

- [ ] **Step 1: Create a reusable layout component for prototype pages**

```vue
<template>
  <a-layout class="prototype-layout">
    <slot />
  </a-layout>
</template>
```

- [ ] **Step 2: Add shared header, sidebar, and section card components**

```vue
<script setup lang="ts">
defineProps<{ title: string; subtitle?: string }>()
</script>
```

- [ ] **Step 3: Verify shared components render in one sample route**

Run: `pnpm exec vue-tsc --noEmit`
Expected: type check passes

### Task 3: Implement userWeb Prototype Pages

**Files:**
- Create: `src/views/user/UserDashboardView.vue`
- Create: `src/views/user/AppGalleryView.vue`
- Create: `src/views/user/BidReviewView.vue`
- Create: `src/views/user/StandardQueryView.vue`
- Create: `src/views/user/PersonalCenterView.vue`
- Create: `src/views/user/ApiManagementView.vue`
- Create: `src/mocks/user-data.ts`

- [ ] **Step 1: Add static mock data for cards, tasks, files, and query results**

```ts
export const quickTasks = [
  { title: '开始 AI 审标', tag: '专业业务' },
  { title: '查询标准条款', tag: '知识查询' }
]
```

- [ ] **Step 2: Build the user dashboard and gallery with workbench-style cards**

```vue
<SectionCard title="推荐任务">
  <a-row :gutter="[16, 16]">...</a-row>
</SectionCard>
```

- [ ] **Step 3: Build `AI审标` and `标准查询` pages with multi-panel layouts**

```vue
<a-steps :current="2" direction="vertical">...</a-steps>
```

- [ ] **Step 4: Build personal center and API management prototype pages**

```vue
<a-descriptions bordered :column="2">...</a-descriptions>
```

- [ ] **Step 5: Verify user routes type check**

Run: `pnpm exec vue-tsc --noEmit`
Expected: passes with no errors from `src/views/user`

### Task 4: Implement adminWeb Prototype Pages

**Files:**
- Create: `src/views/admin/AdminDashboardView.vue`
- Create: `src/views/admin/PermissionView.vue`
- Create: `src/views/admin/ApplicationManagementView.vue`
- Create: `src/views/admin/DataGovernanceView.vue`
- Create: `src/views/admin/AnalyticsView.vue`
- Create: `src/mocks/admin-data.ts`

- [ ] **Step 1: Add static mock data for permissions, applications, and analytics**

```ts
export const metrics = [
  { label: '今日调用量', value: '12,480' }
]
```

- [ ] **Step 2: Build admin dashboard with metric cards and alerts**

```vue
<a-statistic title="今日调用量" :value="12480" />
```

- [ ] **Step 3: Build permissions, application, data governance, and analytics pages**

```vue
<a-table :columns="columns" :data-source="rows" :pagination="false" />
```

- [ ] **Step 4: Verify admin routes type check**

Run: `pnpm exec vue-tsc --noEmit`
Expected: passes with no errors from `src/views/admin`

### Task 5: Routing, Styling, and Final Verification

**Files:**
- Modify: `src/router/index.ts`
- Modify: `src/style.less`
- Modify: `src/App.vue`

- [ ] **Step 1: Add route definitions for `userWeb` and `adminWeb` prototype navigation**

```ts
{
  path: '/user/dashboard',
  component: UserDashboardView
}
```

- [ ] **Step 2: Add Less styles for layout, cards, panels, and prototype polish**

```less
.page-grid {
  display: grid;
  gap: 16px;
}
```

- [ ] **Step 3: Run final type and build verification**

Run: `pnpm exec vue-tsc --noEmit`
Expected: PASS

Run: `pnpm run build`
Expected: Vite production build succeeds
