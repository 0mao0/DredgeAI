import { bootstrapApp } from '@shared/web'
import App from './App.vue'
import router from './router'

// 登录页已接管鉴权（cookie 持久化 + refresh_token 自动续期），dev 临时自动登录方案移除
void bootstrapApp({
  App,
  router,
  loadMock: () => import('./mock'),
})
