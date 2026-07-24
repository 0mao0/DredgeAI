import { createRouter, createWebHistory } from 'vue-router'
import AdminLayout from '@/layouts/AdminLayout.vue'
import { installGuards } from '@shared/web/router'
import { manifestToRoutes } from '@shared/web/router/manifest'
import { adminAppManifests } from './manifests'

/**
 * admin-web 路由表由 manifest 数组动态生成。
 * 如需新增页面，只需在 manifests.ts 中追加 AppManifest 条目。
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
  ],
})

installGuards(router, { appName: '智浚 AI · 管理后台' })

export default router
