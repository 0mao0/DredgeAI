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
        { path: 'bid-review', name: 'BidReview', component: () => import('@/views/bid-review/index.vue'), meta: { title: 'AI 审标' } },
        { path: 'standards', name: 'Standards', component: () => import('@/views/standards/index.vue'), meta: { title: '标准查询' } },
        { path: 'profile', name: 'Profile', component: () => import('@/views/profile/index.vue'), meta: { title: '个人中心' } },
        { path: 'api', name: 'ApiManage', component: () => import('@/views/api/index.vue'), meta: { title: 'API 管理' } },
        // 占位路由：admin-web 已分配的应用模块，页面待开发
        { path: 'knowledge', name: 'Knowledge', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: '知识检索' } },
        { path: 'approval', name: 'Approval', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: '智能审批' } },
        { path: 'insight', name: 'Insight', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: '数据洞察' } },
        { path: 'compliance', name: 'Compliance', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: '合规检查' } },
        { path: 'safety', name: 'Safety', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: '安全监控' } },
        { path: 'cost', name: 'Cost', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: '成本分析' } },
        { path: 'qa', name: 'Qa', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: '知识问答' } },
      ],
    },
  ],
})

router.afterEach((to) => {
  const title = to.meta.title as string | undefined
  document.title = title ? `${title} · 智浚 AI` : '智浚 AI · 用户端'
})

export default router
