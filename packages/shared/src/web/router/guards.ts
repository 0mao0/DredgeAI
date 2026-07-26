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

/**
 * 创建权限守卫：路由声明 meta.requiresPermission 时校验权限码集合。
 * - 权限码 '*' 为通配（超级管理员）
 * - getPermissions 支持异步（首次进入时需等待用户资料加载）
 */
export function createPermissionGuard(
  getPermissions: () => string[] | Promise<string[]>,
  fallback = '/',
): NavigationGuardWithThis<undefined> {
  return async (to) => {
    const required = to.meta.requiresPermission
    if (!required) return true
    const perms = await Promise.resolve(getPermissions())
    if (perms.includes('*') || perms.includes(required)) return true
    return { path: fallback }
  }
}

/** 创建标题守卫：根据路由 meta.title 设置 document.title */
export function createTitleGuard(appName: string): (to: { meta: { title?: string } }) => void {
  return (to) => {
    const title = to.meta.title
    document.title = title ? `${title} · ${appName}` : appName
  }
}

/**
 * 为 router 安装默认守卫组合（auth + permission + title）
 *  - enableAuth 默认 false：仅当应用存在登录页且 tokenKey 配置时才启用，避免无 login 路由时死循环
 *  - 传入 getPermissions 即启用权限守卫（消费路由 meta.requiresPermission）
 */
export function installGuards(
  router: Router,
  opts: {
    appName: string
    tokenKey?: string
    loginPath?: string
    enableAuth?: boolean
    getPermissions?: () => string[] | Promise<string[]>
    permissionFallback?: string
  },
): void {
  if (opts.enableAuth && opts.tokenKey) {
    router.beforeEach(createAuthGuard(opts.tokenKey, opts.loginPath))
  }
  if (opts.getPermissions) {
    router.beforeEach(createPermissionGuard(opts.getPermissions, opts.permissionFallback))
  }
  router.afterEach(createTitleGuard(opts.appName))
}
