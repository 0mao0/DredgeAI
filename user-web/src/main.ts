import { createApp } from 'vue'
import { createPinia } from 'pinia'
import piniaPluginPersistedstate from 'pinia-plugin-persistedstate'
import Antd, { message } from 'ant-design-vue'

import 'ant-design-vue/dist/reset.css'
import 'virtual:uno.css'
import App from './App.vue'
import router from './router'
import { registerPermissionDirective } from '@shared/web/directives'
import '@shared/web/styles/reset.less'
import '@shared/web/styles/global.less'

// Mock 仅开发模式动态加载，生产构建中整个 mock 模块图被 tree-shake
if (import.meta.env.DEV) {
  const { registerMock } = await import('./mock')
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
