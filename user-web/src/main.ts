import { bootstrapApp } from '@shared/web'
import App from './App.vue'
import router from './router'
import { STORAGE_TOKEN_KEY } from '@/utils/constants'

/**
 * Dev-only: 本地开发时若浏览器没有 token，自动向 Auth 服务申请 access_token。
 * 这是临时方案，正式环境必须提供真实登录页；生产构建（import.meta.env.DEV=false）不会执行。
 */
async function tryDevAutoLogin(): Promise<void> {
  if (!import.meta.env.DEV) return
  if (typeof localStorage === 'undefined') return
  if (localStorage.getItem(STORAGE_TOKEN_KEY)) return

  const username = (import.meta.env as any).VITE_DEV_LOGIN_USERNAME as string | undefined
  const password = (import.meta.env as any).VITE_DEV_LOGIN_PASSWORD as string | undefined
  if (!username || !password) return

  try {
    const res = await fetch('/connect/token', {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        grant_type: 'password',
        username,
        password,
        client_id: 'DredgeAI_App',
        scope: 'DredgeAI',
      }),
    })
    if (!res.ok) {
      console.warn('[dev] auto-login failed:', res.status, await res.text())
      return
    }
    const data = await res.json()
    if (data.access_token) {
      localStorage.setItem(STORAGE_TOKEN_KEY, data.access_token)
      window.location.reload()
    }
  } catch (e) {
    console.warn('[dev] auto-login error:', e)
  }
}

void tryDevAutoLogin().then(() =>
  bootstrapApp({
    App,
    router,
    loadMock: () => import('./mock'),
  }),
)
