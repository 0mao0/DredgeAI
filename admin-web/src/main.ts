import { createApp } from 'vue'
import { createPinia } from 'pinia'
import piniaPluginPersistedstate from 'pinia-plugin-persistedstate'
import Antd from 'ant-design-vue'
import 'ant-design-vue/dist/reset.css'
import 'virtual:uno.css'
import App from './App.vue'
import router from './router'
import '@shared/styles/reset.less'
import '@shared/styles/global.less'
import { registerMock } from './mock'

// 注册 mock 路由（仅在开发模式生效，由 USE_MOCK 控制）
registerMock()

const app = createApp(App)
const pinia = createPinia()
pinia.use(piniaPluginPersistedstate)

app.use(pinia)
app.use(router)
app.use(Antd)
app.mount('#app')
