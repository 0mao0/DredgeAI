import type { AppCard } from '@shared/types'
import { mockApplications } from './applications'

/**
 * user-web 可见的应用目录：由 admin 模块发布状态推导。
 * - 若模块定义了 subApps：仅 status==='published' 的子应用进入用户端目录
 * - 若模块无 subApps：模块本身即作为用户端应用
 * 这样 admin 应用控制里的发布/下架开关会直接决定 user-web 能否看到该应用。
 */

// 普通模块（无子应用）在 user-web 侧边栏对应的路由
const MODULE_ROUTES: Record<string, string> = {
  'ff1e69b2-ddfd-4406-b8f9-024db34081cd': '/standard-query',
  '70b2ce01-f400-4947-bcf4-0d5f01568783': '/ai-video',
  '9076f8d6-53a3-490e-b28b-43f69eadf11e': '/ai-dubbing',
  '293781c9-841b-428f-ab9d-f3c3117ce138': '/design-experience',
  'fc005209-8641-4fd9-95f9-9acb632912b6': '/construction-experience',
  '0283801c-7a32-4dc0-96d3-eaee1b1f4ee2': '/construction-review',
  '6dd9583d-169d-43d1-acd0-c1432248b898': '/dredge-efficiency',
  '8da83abb-fdc5-480a-87cc-dca712243ffd': '/ai-bid',
  '50e3cb9e-76a1-40ca-8034-060e7255bf5f': '/ai-meeting',
}

function buildUserApps(): AppCard[] {
  const cards: AppCard[] = []
  for (const app of mockApplications) {
    if (app.subApps && app.subApps.length > 0) {
      for (const sub of app.subApps) {
        if (sub.status !== 'published') continue
        cards.push({
          id: sub.id,
          parentAppId: sub.parentAppId,
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
        status: app.status === 'offline' ? '已下架' : '已授权',
        route: MODULE_ROUTES[app.id] || '',
        version: app.version,
        pinned: false,
      })
    }
  }
  return cards
}

/**
 * 10 个用户端应用模块，与 admin-web 共享同一套发布数据。测试用户已全部授权，默认勾选 8 个放入侧边栏。
 * 分类：通用(3) | 设计(1) | 施工(4) | 经营(2)
 */
export const appCards: AppCard[] = buildUserApps()

/** 默认上架到侧边栏的路由（由应用控制发布后的子应用路由 + 普通模块路由） */
export const DEFAULT_SIDEBAR_ROUTES = [
  '/standard-query',
  '/ai-video',
  '/ai-dubbing',
  '/dredge-efficiency',
  '/ai-bid',
  '/ai-meeting',
  '/intelligence/dredge',
  '/intelligence/tech',
]
