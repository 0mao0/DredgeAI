import { createRouter, createWebHistory } from 'vue-router'
import AdminLayout from '@/layouts/AdminLayout.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      component: AdminLayout,
      redirect: '/dashboard',
      children: [
        { path: 'dashboard', name: 'Dashboard', component: () => import('@/views/dashboard/index.vue'), meta: { title: '仪表盘' } },
        { path: 'permissions', name: 'Permissions', component: () => import('@/views/permissions/index.vue'), meta: { title: '权限管理' } },
        { path: 'applications', name: 'Applications', component: () => import('@/views/applications/index.vue'), meta: { title: '应用管理' } },
        { path: 'data', name: 'DataSources', component: () => import('@/views/data/index.vue'), meta: { title: '数据源' } },
        { path: 'analytics', name: 'Analytics', component: () => import('@/views/analytics/index.vue'), meta: { title: '数据分析' } },
        { path: 'profile', name: 'Profile', component: () => import('@/views/profile/index.vue'), meta: { title: '个人中心' } },
        { path: 'api', name: 'ApiManage', component: () => import('@/views/api/index.vue'), meta: { title: 'API 管理' } },
      ],
    },
  ],
})

router.afterEach((to) => {
  const title = to.meta.title as string | undefined
  document.title = title ? `${title} · 智浚 AI` : '智浚 AI · 管理后台'
})

export default router
