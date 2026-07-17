import { createRouter, createWebHistory } from 'vue-router'
import UserLayout from '@/layouts/UserLayout.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      component: UserLayout,
      redirect: '/dashboard',
      children: [
        { path: 'dashboard', name: 'UserDashboard', component: () => import('@/views/dashboard/index.vue'), meta: { title: '工作台' } },
        { path: 'apps', name: 'UserApps', component: () => import('@/views/apps/index.vue'), meta: { title: '应用广场' } },
        { path: 'bid-review', name: 'BidReview', component: () => import('@/views/bid-review/index.vue'), meta: { title: 'AI 审标' } },
        { path: 'standards', name: 'Standards', component: () => import('@/views/standards/index.vue'), meta: { title: '标准查询' } },
        { path: 'profile', name: 'Profile', component: () => import('@/views/profile/index.vue'), meta: { title: '个人中心' } },
        { path: 'api', name: 'ApiManage', component: () => import('@/views/api/index.vue'), meta: { title: 'API 管理' } },
      ],
    },
  ],
})

router.afterEach((to) => {
  const title = to.meta.title as string | undefined
  document.title = title ? `${title} · 智浚 AI` : '智浚 AI · 用户端'
})

export default router
