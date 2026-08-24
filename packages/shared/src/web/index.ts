// shared/web：Vue 专属共享层入口
// 仅在 web 端（user-web / admin-web）使用，app 端不应依赖此模块

// 共享组件
export { default as AppButton } from './components/AppButton.vue'
export { default as AudioPlayer } from './components/AudioPlayer.vue'
export { default as ChartContainer } from './components/ChartContainer.vue'
export { default as DataSkeleton } from './components/DataSkeleton.vue'
export { default as DataTable } from './components/DataTable.vue'
export { default as DevelopingHint } from './components/DevelopingHint.vue'
export { default as DocViewer } from './components/DocViewer.vue'
export { default as EmptyState } from './components/EmptyState.vue'
export { default as ErrorBoundary } from './components/ErrorBoundary.vue'
export { default as Logo } from './components/Logo.vue'
export { default as MetricCard } from './components/MetricCard.vue'
export { default as PageHeader } from './components/PageHeader.vue'
export { default as SectionCard } from './components/SectionCard.vue'
export { default as ShipAiLogo } from './components/ShipAiLogo.vue'
export { default as SidebarToggleIcon } from './components/SidebarToggleIcon.vue'
export { default as StandardDetailDrawer } from './components/StandardDetailDrawer.vue'
export { default as StandardPdfViewer } from './components/StandardPdfViewer.vue'
export { default as ThemeToggle } from './components/ThemeToggle.vue'
export { default as UploadFileRow } from './components/UploadFileRow.vue'
export { default as VoiceRegisterModal } from './components/VoiceRegisterModal.vue'
export type { DataTableColumn, DataTableFilter } from './components/DataTable.vue'

// 共享应用图标（admin 选择器 / user 渲染共用）
export { APP_ICONS, resolveAppIcon } from './utils/appIcons'

// 共享 composables
export { useCssVar } from './composables/useCssVar'
export { useTheme } from './composables/useTheme'

// 共享 stores
export * from './stores'

// 共享路由守卫
export * from './router'

// 共享指令
export * from './directives'

// 共享 http 工具
export { createWebRequest } from './http/createWebRequest'

// 应用启动封装
export * from './bootstrap'
