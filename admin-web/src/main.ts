import { bootstrapApp } from '@shared/web'
import App from './App.vue'
import router from './router'

void bootstrapApp({
  App,
  router,
  loadMock: () => import('./mock'),
})
