import type { Component } from 'vue'
import type { RouteRecordRaw } from 'vue-router'
import type { AppManifest } from '../../core/types/application'

/**
 * 将 AppManifest 数组转换为 vue-router 路由记录。
 * 支持：
 * - 嵌套 children（分组路由）
 * - redirect（重定向）
 * - meta 透传（title、icon、parentKeys 等）
 */
export function manifestToRoutes(manifests: AppManifest[]): RouteRecordRaw[] {
  return manifests.map((m) => {
    const route: Record<string, unknown> = {
      path: m.route.replace(/^\//, ''),
      name: m.name,
      meta: {
        title: m.title,
        icon: m.icon,
        category: m.category,
        parentKeys: m.parentKeys,
        requiresPermission: m.requiredPermission,
      },
    }

    if (m.redirect) route.redirect = m.redirect
    if (m.component) route.component = m.component as () => Promise<Component>
    if (m.children && m.children.length > 0) route.children = manifestToRoutes(m.children)

    return route as unknown as RouteRecordRaw
  })
}
