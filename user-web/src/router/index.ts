import { createRouter, createWebHistory } from 'vue-router'
import UserLayout from '@/layouts/UserLayout.vue'
import { installGuards, manifestToRoutes } from '@shared/web/router'
import { userAppManifests } from './manifests'
import { useUserStore } from '@/stores/user'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      component: UserLayout,
      redirect: '/dashboard',
      children: manifestToRoutes(userAppManifests),
    },
  ],
})

installGuards(router, {
  appName: '智浚AI',
  getPermissions: async () => {
    const store = useUserStore()
    try {
      await store.ensureUser()
    } catch {
      // 权限加载失败时按无权限处理，已声明 requiresPermission 的页面将被拦截
    }
    return store.userInfo?.authorizedScopes ?? []
  },
})

export default router
