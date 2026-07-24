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
    if (visibleAppRoutes.value.includes(route)) {
      visibleAppRoutes.value = visibleAppRoutes.value.filter((r) => r !== route)
    } else {
      visibleAppRoutes.value = [...visibleAppRoutes.value, route]
    }
  }

  function isRouteVisible(route: string): boolean {
    return sidebarAppsSet.value.has(route)
  }

  /** 默认勾选应用显示在侧边栏：通用3 + 施工2 + 经营2(情报采集子应用) */
  const DEFAULT_VISIBLE_ROUTES = ['/standard-query', '/ai-video', '/ai-dubbing', '/dredge-efficiency', '/ai-bid', '/intelligence/dredge', '/intelligence/tech']

  async function fetchApps(): Promise<void> {
    apps.value = await getAppList()
    const routesWithRoute = authorizedApps.value
      .filter((a) => a.route)
      .map((a) => a.route!)

    // 过滤掉失效路由（应用被取消授权等），保留其余用户已勾选的路由
    visibleAppRoutes.value = visibleAppRoutes.value.filter((r) => routesWithRoute.includes(r))
    // 仅在结果为空时回退到默认值
    if (visibleAppRoutes.value.length === 0) {
      visibleAppRoutes.value = DEFAULT_VISIBLE_ROUTES.filter((r) => routesWithRoute.includes(r))
    }
  }
  async function fetchTasks(): Promise<void> { tasks.value = await getRecentTasks() }
  async function fetchQuickTasks(): Promise<void> { quickTasks.value = await getQuickTasks() }
  async function fetchFiles(): Promise<void> { files.value = await getRecentFiles() }

  return {
    apps, tasks, quickTasks, files,
    visibleAppRoutes, authorizedApps, sidebarApps,
    setVisibleRoutes, toggleAppRoute, isRouteVisible,
    fetchApps, fetchTasks, fetchQuickTasks, fetchFiles,
  }
}, {
  persist: { pick: ['visibleAppRoutes'] },
})
