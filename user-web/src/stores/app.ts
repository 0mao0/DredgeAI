import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { AppCard, TaskItem, FileItem } from '@/types'
import { getAppList } from '@/api/modules/app'
import { getRecentTasks, getQuickTasks } from '@/api/modules/task'
import { getRecentFiles } from '@/api/modules/file'

export interface SidebarApp {
  route: string
  title: string
  icon: string
}

export const useAppStore = defineStore('app', () => {
  const apps = ref<AppCard[]>([])
  const tasks = ref<TaskItem[]>([])
  const quickTasks = ref<{ id: string; title: string; tag: string; route: string; icon: string }[]>([])
  const files = ref<FileItem[]>([])
  const sidebarCollapsed = ref(false)
  const visibleAppRoutes = ref<string[]>([])

  const authorizedApps = computed(() =>
    apps.value.filter((a) => a.status === '已授权')
  )

  const sidebarApps = computed(() =>
    authorizedApps.value
      .filter((a) => a.route && visibleAppRoutes.value.includes(a.route))
      .sort((a, b) => {
        const ai = visibleAppRoutes.value.indexOf(a.route!)
        const bi = visibleAppRoutes.value.indexOf(b.route!)
        return ai - bi
      })
      .map((a): SidebarApp => ({ route: a.route!, title: a.title, icon: a.icon }))
  )

  const sidebarAppsSet = computed(() => new Set(sidebarApps.value.map((a) => a.route)))

  function setVisibleRoutes(routes: string[]): void {
    visibleAppRoutes.value = routes
  }

  function toggleAppRoute(route: string): void {
    const set = new Set(visibleAppRoutes.value)
    if (set.has(route)) {
      set.delete(route)
      visibleAppRoutes.value = visibleAppRoutes.value.filter((r) => r !== route)
    } else {
      visibleAppRoutes.value = [...visibleAppRoutes.value, route]
    }
  }

  function isRouteVisible(route: string): boolean {
    return sidebarAppsSet.value.has(route)
  }

  /** 默认勾选 3 个应用显示在侧边栏：标书智能分析、图纸合规检查、知识检索系统 */
  const DEFAULT_VISIBLE_ROUTES = ['/bid-review', '/standards', '/knowledge']

  async function fetchApps(): Promise<void> {
    apps.value = await getAppList()
    const routesWithRoute = authorizedApps.value
      .filter((a) => a.route)
      .map((a) => a.route!)

    // 迁移：如果持久化的路由在当前应用中不存在，重置为默认值
    const hasStaleRoutes = visibleAppRoutes.value.length > 0
      && visibleAppRoutes.value.some((r) => !routesWithRoute.includes(r))

    if (visibleAppRoutes.value.length === 0 || hasStaleRoutes) {
      visibleAppRoutes.value = DEFAULT_VISIBLE_ROUTES.filter((r) => routesWithRoute.includes(r))
    }
  }
  async function fetchTasks(): Promise<void> { tasks.value = await getRecentTasks() }
  async function fetchQuickTasks(): Promise<void> { quickTasks.value = await getQuickTasks() }
  async function fetchFiles(): Promise<void> { files.value = await getRecentFiles() }
  function toggleSidebar(): void { sidebarCollapsed.value = !sidebarCollapsed.value }

  return {
    apps, tasks, quickTasks, files, sidebarCollapsed,
    visibleAppRoutes, authorizedApps, sidebarApps,
    setVisibleRoutes, toggleAppRoute, isRouteVisible,
    fetchApps, fetchTasks, fetchQuickTasks, fetchFiles, toggleSidebar,
  }
}, {
  persist: { pick: ['sidebarCollapsed', 'visibleAppRoutes'] },
})
