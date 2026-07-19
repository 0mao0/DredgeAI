// shared 包入口：保持向后兼容的扁平导出
// 内部目录已分层为 core/（框架无关）和 web/（Vue 专属）

// 共享组件
export { default as ChartContainer } from './web/components/ChartContainer.vue'
export { default as DataSkeleton } from './web/components/DataSkeleton.vue'
export { default as Logo } from './web/components/Logo.vue'
export { default as MetricCard } from './web/components/MetricCard.vue'
export { default as PageHeader } from './web/components/PageHeader.vue'
export { default as SectionCard } from './web/components/SectionCard.vue'
export { default as ThemeToggle } from './web/components/ThemeToggle.vue'

// 共享 composables
export { useCssVar } from './web/composables/useCssVar'
export { useTheme } from './web/composables/useTheme'

// 共享工具函数（已迁移至 core/utils）
export * from './core/utils'

// 共享类型
export * from './types'
