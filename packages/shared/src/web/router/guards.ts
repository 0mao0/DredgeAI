import type { NavigationGuardWithThis, Router } from 'vue-router'

/** 路由 meta 中可声明的权限字段 */
declare module 'vue-router' {
  interface RouteMeta {
    /** 页面标题（用于 document.title） */
    title?: string
    /** 访问所需权限码，留空表示公开 */
    requiresPermission?: string
    /** 是否需要登录（默认 true） */
    requiresAuth?: boolean
  }
}

/** 创建登录守卫：未携带 token 时重定向到 loginPath */
export function createAuthGuard(tokenKey: string, loginPath = '/login'): NavigationGuardWithThis<undefined> {
  return (to) => {
    if (to.meta.requiresAuth === false) return true
    const token = typeof localStorage !== 'undefined' ? localStorage.getItem(tokenKey) : null
    if (!token) {
      return { path: loginPath, query: { redirect: to.fullPath } }
    }
    return true
  }
}

/** 创建标题守卫：根据路由 meta.title 设置 document.title */
export function createTitleGuard(appName: string): (to: { meta: { title?: string } }) => void {
  return (to) => {
    const title = to.meta.title
    document.title = title ? `${title} · ${appName}` : appName
  }
}

/** 为 router 安装默认守卫组合（auth + title）
 *  - enableAuth 默认 false：仅当应用存在登录页且 tokenKey 配置时才启用，避免无 login 路由时死循环
 */
export function installGuards(
  router: Router,
  opts: { appName: string; tokenKey?: string; loginPath?: string; enableAuth?: boolean },
): void {
  if (opts.enableAuth && opts.tokenKey) {
    router.beforeEach(createAuthGuard(opts.tokenKey, opts.loginPath))
  }
  router.afterEach(createTitleGuard(opts.appName))
}
