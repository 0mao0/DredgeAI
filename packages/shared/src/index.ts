// shared 包入口：保持向后兼容的扁平导出
// 内部目录已分层为 core/（框架无关）和 web/（Vue 专属）

// Vue 专属共享层（组件 / composables / stores / 指令）
export * from './web'

// 框架无关内核（类型 / http / api / utils）
export * from './core'

// 兼容旧路径 @shared/types
export * from './types'
