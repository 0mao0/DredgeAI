import { createApp } from 'vue'
import type { Component } from 'vue'
import { createPinia } from 'pinia'
import piniaPluginPersistedstate from 'pinia-plugin-persistedstate'
import Antd, { message } from 'ant-design-vue'
import type { Router } from 'vue-router'
import { registerPermissionDirective } from './directives'

import 'ant-design-vue/dist/reset.css'
import 'virtual:uno.css'
import './styles/reset.less'
import './styles/global.less'

export interface BootstrapAppOptions {
  App: Component
  router: Router
  /** Mock 仅开发模式动态加载，生产构建中整个 mock 模块图被 tree-shake */
  loadMock?: () => Promise<{ registerMock: () => void }>
}

export async function bootstrapApp({ App, router, loadMock }: BootstrapAppOptions): Promise<void> {
  if (import.meta.env.DEV && loadMock) {
    const { registerMock } = await loadMock()
    registerMock()
  }

  // 全局未捕获 Promise 异常兜底
  window.addEventListener('unhandledrejection', (event) => {
    console.error('Unhandled promise rejection:', event.reason)
    message.error('系统异常，请刷新页面重试')
  })

  const app = createApp(App)
  const pinia = createPinia()
  pinia.use(piniaPluginPersistedstate)

  app.use(pinia)
  app.use(router)
  app.use(Antd)
  registerPermissionDirective(app)
  app.mount('#app')
}
