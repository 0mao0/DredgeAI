import type { AppCard } from '@shared/types'
import { mockApplications } from './applications'

/**
 * user-web 可见的应用目录：由 admin 模块发布状态推导。
 * - 若模块定义了 subApps：仅 status==='已发布' 的子应用进入用户端目录
 * - 若模块无 subApps：模块本身即作为用户端应用
 * 这样 admin 应用控制里的发布/下架开关会直接决定 user-web 能否看到该应用。
 */

// 普通模块（无子应用）在 user-web 侧边栏对应的路由
const MODULE_ROUTES: Record<string, string> = {
  '1': '/standard-query',
  '2': '/ai-video',
  '3': '/ai-dubbing',
  '4': '/design-experience',
  '5': '/construction-experience',
  '6': '/construction-review',
  '7': '/dredge-efficiency',
  '9': '/bid-review',
}

function buildUserApps(): AppCard[] {
  const cards: AppCard[] = []
  for (const app of mockApplications) {
    if (app.subApps && app.subApps.length > 0) {
      for (const sub of app.subApps) {
        if (sub.status !== '已发布') continue
        cards.push({
          id: sub.id,
          title: sub.name,
          description: sub.description || `${app.name}的子应用`,
          category: sub.category,
          icon: sub.icon,
          status: '已授权',
          route: sub.route,
          version: sub.version,
          pinned: false,
        })
      }
    } else {
      cards.push({
        id: app.id,
        title: app.name,
        description: `${app.name}应用模块`,
        category: app.category,
        icon: app.icon,
        status: app.status === '已下架' ? '已下架' : '已授权',
        route: MODULE_ROUTES[app.id] || '',
        version: app.version,
        pinned: false,
      })
    }
  }
  return cards
}

/** 9 个用户端应用模块，与 admin-web 共享同一套发布数据。测试用户已全部授权，默认勾选 5 个放入侧边栏。
 * 分类：通用(3) | 设计(1) | 施工(3) | 经营(2)
 */
export const appCards: AppCard[] = buildUserApps()

/** 默认上架到侧边栏的路由（由应用控制发布后的子应用路由 + 普通模块路由） */
export const DEFAULT_SIDEBAR_ROUTES = [
  '/standard-query',
  '/ai-video',
  '/ai-dubbing',
  '/dredge-efficiency',
  '/bid-review',
  '/intelligence/dredge',
  '/intelligence/tech',
]
