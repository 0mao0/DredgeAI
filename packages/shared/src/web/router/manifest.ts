import type { Component } from 'vue'
import type { RouteRecordRaw } from 'vue-router'
import type { AppManifest } from '../../core/types/application'

/** 将 AppManifest 数组转换为 vue-router 子路由记录 */
export function manifestToRoutes(manifests: AppManifest[]): RouteRecordRaw[] {
  return manifests.map((m) => ({
    path: m.route.replace(/^\//, ''),
    name: m.name,
    component: m.component as () => Promise<Component>,
    meta: {
      title: m.title,
      requiresPermission: m.requiredPermission,
    },
  }))
}

/** 过滤出默认可见的应用路由（用于侧边栏默认勾选） */
export function getDefaultVisibleRoutes(manifests: AppManifest[]): string[] {
  return manifests.filter((m) => m.defaultVisible).map((m) => m.route)
}
