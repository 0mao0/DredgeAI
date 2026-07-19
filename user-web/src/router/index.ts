import { createRouter, createWebHistory } from 'vue-router'
import UserLayout from '@/layouts/UserLayout.vue'
import { installGuards, manifestToRoutes } from '@shared/web/router'
import { userAppManifests } from './manifests'

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

installGuards(router, { appName: '智浚 AI · 用户端' })

export default router
