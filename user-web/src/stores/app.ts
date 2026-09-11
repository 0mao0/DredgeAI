import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { AppCard, TaskItem, FileItem } from '@/types'
import { getAppList, getUserAppOrder, saveUserAppOrder } from '@/api/modules/app'
import { getRecentTasks, getQuickTasks } from '@/api/modules/task'
import { getRecentFiles } from '@/api/modules/file'
import { cachedFetch, invalidateRequest } from '@shared/web/composables/useRequest'

export interface SidebarApp {
  id: string
  route: string
  title: string
  icon: string
}

export const useAppStore = defineStore('app', () => {
  const apps = ref<AppCard[]>([])
  const tasks = ref<TaskItem[]>([])
  const quickTasks = ref<{ id: string, title: string, tag: string, route: string, icon: string }[]>([])
  const files = ref<FileItem[]>([])
  const visibleAppRoutes = ref<string[]>([])
  /** 当前用户个性化顺序（route 列表；null = 未个性化，跟随目录全局顺序） */
  const userOrder = ref<string[] | null>(null)

  const authorizedApps = computed(() =>
    apps.value.filter((a) => a.status === '已授权'),
  )

  /** 合并顺序：个性化优先；未个性化按目录返回顺序（后端已按全局排序行排序）；新应用按目录顺序稳定插入，不破坏已有相对顺序 */
  function mergeVisibleOrder(visible: AppCard[]): AppCard[] {
    // 默认顺序基准：apps 数组顺序（/list 已按全局排序行排序，且同母项子应用卡片相邻）
    const adminPos = new Map(apps.value.map((a, i) => [a.parentAppId ?? a.id, i]))
    const adminKeyOf = (a: AppCard): string => a.parentAppId ?? a.id
    const subIdxOf = (a: AppCard): number => {
      if (!a.parentAppId) return 0
      // 同母项卡片在 apps 中的相对下标
      const siblings = apps.value.filter((x) => x.parentAppId === a.parentAppId)
      const i = siblings.findIndex((x) => x.id === a.id)
      return i === -1 ? Number.MAX_SAFE_INTEGER : i
    }
    const compareByOrder = (a: AppCard, b: AppCard): number => {
      const ai = adminPos.get(adminKeyOf(a)) ?? Number.MAX_SAFE_INTEGER
      const bi = adminPos.get(adminKeyOf(b)) ?? Number.MAX_SAFE_INTEGER
      if (ai !== bi) return ai - bi
      return subIdxOf(a) - subIdxOf(b)
    }
    const personalized = userOrder.value
    if (!personalized || personalized.length === 0) {
      return [...visible].sort(compareByOrder)
    }

    const personalizedSet = new Set(personalized)
    const existing = visible
      .filter((a) => a.route && personalizedSet.has(a.route))
      .sort((a, b) => personalized.indexOf(a.route!) - personalized.indexOf(b.route!))
    const rest = visible
      .filter((a) => !a.route || !personalizedSet.has(a.route))
      .sort(compareByOrder)

    const ordered = [...existing]
    for (const app of rest) {
      const pos = adminPos.get(adminKeyOf(app)) ?? Number.MAX_SAFE_INTEGER
      let insertAt = ordered.length
      let lastBeforeOrEqual = -1
      for (let i = 0; i < ordered.length; i += 1) {
        const current = adminPos.get(adminKeyOf(ordered[i])) ?? Number.MAX_SAFE_INTEGER
        if (current <= pos) {
          lastBeforeOrEqual = i
        } else if (current > pos) {
          insertAt = i
          break
        }
      }
      if (insertAt === ordered.length) insertAt = lastBeforeOrEqual + 1
      ordered.splice(insertAt, 0, app)
    }
    return ordered
  }

  const sidebarApps = computed(() =>
    mergeVisibleOrder(
      authorizedApps.value.filter((a) => a.route && visibleAppRoutes.value.includes(a.route)),
    ).map((a): SidebarApp => ({ id: a.id, route: a.route!, title: a.title, icon: a.icon })),
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

  /** 默认勾选应用显示在侧边栏：通用3 + 施工3(含AI晨会) + 经营2(情报采集子应用) */
  const DEFAULT_VISIBLE_ROUTES = ['/standard-query', '/ai-video', '/ai-dubbing', '/dredge-efficiency', '/ai-bid', '/ai-meeting', '/intelligence/dredge', '/intelligence/tech']

  /** 拉取当前用户个性化顺序；后端不可用时退化为本地顺序 */
  async function refreshAppOrders(): Promise<void> {
    try {
      const userRes = await getUserAppOrder()
      userOrder.value = userRes.routeIds ?? null
    } catch {
      // 顺序服务未启动：用本地 visibleAppRoutes 作为个性化顺序，保持原有行为
      userOrder.value = visibleAppRoutes.value.length > 0 ? [...visibleAppRoutes.value] : null
    }
  }

  /** 保存用户个性化顺序（写后端 + 更新本地） */
  async function saveUserOrder(routes: string[]): Promise<void> {
    userOrder.value = routes
    try {
      userOrder.value = (await saveUserAppOrder(routes)).routeIds ?? routes
    } catch {
      // 后端不可用时仅保留本地顺序
    }
  }

  async function fetchApps(force = false): Promise<void> {
    const cacheKey = 'user:app-list'
    if (force) invalidateRequest(cacheKey)
    apps.value = await cachedFetch(cacheKey, getAppList)
    const routesWithRoute = authorizedApps.value
      .filter((a) => a.route)
      .map((a) => a.route!)

    // 过滤掉失效路由（应用被取消授权等），保留其余用户已勾选的路由
    visibleAppRoutes.value = visibleAppRoutes.value.filter((r) => routesWithRoute.includes(r))
    // 仅在结果为空时回退到默认值
    if (visibleAppRoutes.value.length === 0) {
      visibleAppRoutes.value = DEFAULT_VISIBLE_ROUTES.filter((r) => routesWithRoute.includes(r))
    }
    await refreshAppOrders()
  }
  async function fetchTasks(): Promise<void> { tasks.value = await getRecentTasks() }
  async function fetchQuickTasks(): Promise<void> { quickTasks.value = await getQuickTasks() }
  async function fetchFiles(): Promise<void> { files.value = await getRecentFiles() }

  return {
    apps,
    tasks,
    quickTasks,
    files,
    visibleAppRoutes,
    userOrder,
    authorizedApps,
    sidebarApps,
    setVisibleRoutes,
    toggleAppRoute,
    isRouteVisible,
    refreshAppOrders,
    saveUserOrder,
    fetchApps,
    fetchTasks,
    fetchQuickTasks,
    fetchFiles,
  }
}, {
  persist: { pick: ['visibleAppRoutes'] },
})
