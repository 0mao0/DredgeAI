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
        { path: 'menu-config', name: 'MenuConfig', component: () => import('@/views/dev/menu-config.vue'), meta: { title: '菜单配置' } },
        { path: 'task-scheduler', name: 'TaskScheduler', component: () => import('@/views/dev/task-scheduler.vue'), meta: { title: '任务调度' } },
        { path: 'logs', name: 'Logs', component: () => import('@/views/dev/logs.vue'), meta: { title: '日志管理' } },
        { path: 'platform', name: 'Platform', component: () => import('@/views/dev/platform.vue'), meta: { title: '平台信息' } },
        { path: 'org-users', name: 'OrgUsers', component: () => import('@/views/org-users/index.vue'), meta: { title: '组织用户' } },
        { path: 'permissions', name: 'Permissions', component: () => import('@/views/permissions/index.vue'), meta: { title: '权限管理' } },
        { path: 'dashboard', name: 'Dashboard', component: () => import('@/views/dashboard/index.vue'), meta: { title: '仪表盘' } },
        { path: 'api', name: 'ApiManage', component: () => import('@/views/api/index.vue'), meta: { title: 'API 管理' } },
        { path: 'applications', redirect: '/applications/analysis', children: [
          { path: 'analysis', component: () => import('@/views/applications/analysis.vue'), meta: { title: '数据分析' } },
          { path: 'control', component: () => import('@/views/applications/control.vue'), meta: { title: '发布管理' } },
          { path: ':id', component: () => import('@/views/applications/detail.vue') },
        ] },
        { path: 'data', redirect: '/data/statistics', children: [
          { path: 'statistics', component: () => import('@/views/data/statistics.vue'), meta: { title: '数据统计' } },
          { path: 'dynamic/monitoring', component: () => import('@/views/data/dynamic/monitoring.vue'), meta: { title: '监控' } },
          { path: 'dynamic/tide-level', component: () => import('@/views/data/dynamic/tide-level.vue'), meta: { title: '潮位' } },
          { path: 'static/enterprise', component: () => import('@/views/data/static/enterprise.vue'), meta: { title: '企业库' } },
          { path: 'static/standards', component: () => import('@/views/data/static/standards.vue'), meta: { title: '标准库' } },
          { path: 'static/reports', component: () => import('@/views/data/static/reports.vue'), meta: { title: '报告库' } },
          { path: 'static/experience', component: () => import('@/views/data/static/experience.vue'), meta: { title: '经验库' } },
        ] },
        { path: 'alerts', name: 'Alerts', component: () => import('@/views/alerts/index.vue'), meta: { title: '预警管理' } },
        { path: 'profile', name: 'Profile', component: () => import('@/views/profile/index.vue'), meta: { title: '个人中心' } },
      ],
    },
  ],
})

router.afterEach((to) => {
  const title = to.meta.title as string | undefined
  document.title = title ? `${title} · 智浚 AI` : '智浚 AI · 管理后台'
})

export default router
