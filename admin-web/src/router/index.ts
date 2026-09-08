import { createRouter, createWebHistory } from 'vue-router'
import AdminLayout from '@/layouts/AdminLayout.vue'
import { installGuards } from '@shared/web/router'
import { manifestToRoutes } from '@shared/web/router/manifest'
import { adminAppManifests } from './manifests'
import { useAppStore } from '@/stores/app'
import { useAuthStore } from '@/stores/auth'
import { getCookie } from '@/utils/cookie'
import { LOGIN_PATH, STORAGE_TOKEN_KEY } from '@/utils/constants'

/**
 * admin-web 路由表由 manifest 数组动态生成。
 * 如需新增页面，只需在 manifests.ts 中追加 AppManifest 条目。
 * /login 与 /auth/callback 为独立于 AdminLayout 的全屏路由（登录流程无侧栏/顶栏）。
 */

const children = manifestToRoutes(adminAppManifests)

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      component: AdminLayout,
      redirect: '/dashboard',
      children,
    },
    {
      path: LOGIN_PATH,
      name: 'login',
      component: () => import('@/views/login/index.vue'),
      meta: { requiresAuth: false, title: '登录' },
    },
    {
      path: '/auth/callback',
      name: 'auth-callback',
      component: () => import('@/views/login/AuthCallback.vue'),
      meta: { requiresAuth: false },
    },
  ],
})

// 静默续期前置守卫：进入受保护页但 access token 缺失/临过期时，先等一次 refresh_token
// 换新完成再放行（导航完成前数据请求不会以旧 token 撞 401）。随后由 installGuards 的
// auth 守卫兜底：刷新失败（登录态已清空）时跳登录页。
router.beforeEach(async (to) => {
  if (to.meta.requiresAuth === false) return true
  await useAuthStore().ensureFreshToken()
  return true
})

// auth 守卫先于 permission 守卫注册（installGuards 内部顺序）：未登录先跳登录页，
// 避免未带 token 触发 getPermissions 的 401 噪音。
installGuards(router, {
  appName: '智浚AI',
  enableTitle: false,
  enableAuth: true,
  tokenKey: STORAGE_TOKEN_KEY,
  loginPath: LOGIN_PATH,
  getToken: () => getCookie(STORAGE_TOKEN_KEY),
  getPermissions: async () => {
    const store = useAppStore()
    try {
      await store.fetchProfile()
    } catch {
      return []
    }
    return store.permissions
  },
})

export default router
