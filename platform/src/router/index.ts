import { createRouter, createWebHistory } from 'vue-router'
import UserLayout from '@/layouts/UserLayout.vue'
import AdminLayout from '@/layouts/AdminLayout.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      redirect: '/user/profile',
    },
    {
      path: '/user',
      component: UserLayout,
      children: [
        { path: 'dashboard', redirect: '/user/profile' },
        { path: 'apps', name: 'UserApps', component: () => import('@/views/user/Apps.vue') },
        { path: 'bid-review', name: 'BidReview', component: () => import('@/views/user/BidReview.vue') },
        { path: 'standards', name: 'Standards', component: () => import('@/views/user/Standards.vue') },
        { path: 'profile', name: 'Profile', component: () => import('@/views/user/Profile.vue') },
        { path: 'api', name: 'ApiManage', component: () => import('@/views/user/Api.vue') },
      ],
    },
    {
      path: '/admin',
      component: AdminLayout,
      children: [
        { path: 'dashboard', name: 'AdminDashboard', component: () => import('@/views/admin/Dashboard.vue') },
        { path: 'permissions', name: 'Permissions', component: () => import('@/views/admin/Permissions.vue') },
        { path: 'applications', name: 'AppManagement', component: () => import('@/views/admin/Applications.vue') },
        { path: 'data', name: 'DataGovernance', component: () => import('@/views/admin/Data.vue') },
        { path: 'analytics', name: 'Analytics', component: () => import('@/views/admin/Analytics.vue') },
      ],
    },
  ],
})

export default router
