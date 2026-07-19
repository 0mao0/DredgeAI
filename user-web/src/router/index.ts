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
        { path: 'standard-query', name: 'StandardQuery', component: () => import('@/views/standards/index.vue'), meta: { title: '标准查询' } },
        { path: 'bid-review', name: 'BidReview', component: () => import('@/views/bid-review/index.vue'), meta: { title: '投标审核' } },
        { path: 'profile', name: 'Profile', component: () => import('@/views/profile/index.vue'), meta: { title: '个人中心' } },
        { path: 'api', name: 'ApiManage', component: () => import('@/views/api/index.vue'), meta: { title: 'API 管理' } },
        // 占位路由：应用模块页面待开发
        { path: 'ai-video', name: 'AiVideo', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: 'AI视频' } },
        { path: 'ai-dubbing', name: 'AiDubbing', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: 'AI配音' } },
        { path: 'design-experience', name: 'DesignExperience', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: '设计经验' } },
        { path: 'construction-experience', name: 'ConstructionExperience', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: '施工经验' } },
        { path: 'construction-review', name: 'ConstructionReview', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: '施组审核' } },
        { path: 'dredge-efficiency', name: 'DredgeEfficiency', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: '耙吸效率' } },
        { path: 'intelligence/dredge', name: 'IntelligenceDredge', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: '疏浚情报' } },
        { path: 'intelligence/tech', name: 'IntelligenceTech', component: () => import('@/views/placeholder/PlaceholderView.vue'), meta: { title: '科技情报' } },
      ],
    },
  ],
})

router.afterEach((to) => {
  const title = to.meta.title as string | undefined
  document.title = title ? `${title} · 智浚 AI` : '智浚 AI · 用户端'
})

export default router
