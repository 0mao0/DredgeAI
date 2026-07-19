// shared/web：Vue 专属共享层入口
// 仅在 web 端（user-web / admin-web）使用，app 端不应依赖此模块

// 共享组件
export { default as ChartContainer } from './components/ChartContainer.vue'
export { default as DataSkeleton } from './components/DataSkeleton.vue'
export { default as ErrorBoundary } from './components/ErrorBoundary.vue'
export { default as Logo } from './components/Logo.vue'
export { default as MetricCard } from './components/MetricCard.vue'
export { default as PageHeader } from './components/PageHeader.vue'
export { default as SectionCard } from './components/SectionCard.vue'
export { default as ThemeToggle } from './components/ThemeToggle.vue'

// 共享 composables
export { useCssVar } from './composables/useCssVar'
export { useTheme } from './composables/useTheme'

// 共享 stores
export * from './stores'

// 共享路由守卫
export * from './router'

// 共享指令
export * from './directives'
