import type { Component } from 'vue'
import type { RouteRecordRaw } from 'vue-router'
import type { AppManifest } from '../../core/types/application'

/**
 * 将 AppManifest 数组转换为 vue-router 路由记录。
 * 支持：
 * - 嵌套 children（分组路由）：子路由 path 自动计算为相对路径
 * - redirect（重定向）
 * - meta 透传（title、icon、parentKeys 等）
 */
export function manifestToRoutes(manifests: AppManifest[], parentPath = ''): RouteRecordRaw[] {
  return manifests.map((m) => {
    const fullPath = m.route.replace(/^\//, '')
    // 子路由 path 相对于父路由，计算差值去掉父前缀
    const path = parentPath
      ? fullPath.slice(parentPath.length).replace(/^\//, '') || ''
      : fullPath

    const route: Record<string, unknown> = {
      path,
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
    if (m.children && m.children.length > 0) route.children = manifestToRoutes(m.children, fullPath)

    return route as unknown as RouteRecordRaw
  })
}

/** 菜单节点：由 manifest 推导，icon 为 antd 图标名（由消费方解析为组件） */
export interface MenuNode {
  /** 叶子节点为路由路径；分组节点为 parentKeys 最后一级（与 route.meta.parentKeys 对齐，驱动 openKeys） */
  key: string
  title: string
  icon?: string
  children?: MenuNode[]
}

/** 无 manifest 对应项的纯菜单分组元数据（如 admin 的 dev/users 分组） */
export interface MenuGroupMeta {
  title: string
  icon?: string
}

/**
 * 将 AppManifest 数组转换为菜单树，与 manifestToRoutes 共用同一份数据源，
 * 消除「路由 + Layout 菜单」双重维护。
 *
 * 规则：
 * - menuPlacement = 'bottom' / 'hidden' 或参数化路由（含 :）不进入主菜单
 * - 带 children 的 manifest 即分组定义者（提供分组 title/icon）
 * - 仅带 parentKeys 的叶子 manifest 归入 parentKeys[0] 对应分组；
 *   分组无 manifest 定义者时从 groups 参数取元数据
 * - 输出顺序与 manifest 数组顺序一致，分组在首次遇到时插入
 */
export function manifestToMenu(manifests: AppManifest[], groups: Record<string, MenuGroupMeta> = {}): MenuNode[] {
  const isMenuEntry = (m: AppManifest): boolean =>
    (m.menuPlacement ?? 'main') === 'main' && !m.route.includes(':')

  const groupKeyOf = (m: AppManifest): string =>
    m.parentKeys?.[m.parentKeys.length - 1] ?? m.id

  const groupNodes = new Map<string, MenuNode>()
  const roots: MenuNode[] = []

  function ensureGroup(key: string, meta?: MenuGroupMeta): MenuNode {
    let g = groupNodes.get(key)
    if (!g) {
      g = { key, title: meta?.title ?? key, icon: meta?.icon, children: [] }
      groupNodes.set(key, g)
      roots.push(g)
    } else if (meta && g.title === g.key) {
      // 用分组定义者的元数据补全先前由子项创建的占位分组
      g.title = meta.title
      g.icon = meta.icon ?? g.icon
    }
    return g
  }

  function toNode(m: AppManifest): MenuNode | null {
    if (!isMenuEntry(m)) return null
    if (m.children && m.children.length > 0) {
      const children = m.children.map(toNode).filter((n): n is MenuNode => n !== null)
      return { key: groupKeyOf(m), title: m.title, icon: m.icon, children }
    }
    return { key: m.route, title: m.title, icon: m.icon }
  }

  for (const m of manifests) {
    if (!isMenuEntry(m)) continue
    if (m.children && m.children.length > 0) {
      const g = ensureGroup(groupKeyOf(m), { title: m.title, icon: m.icon })
      g.children!.push(...m.children.map(toNode).filter((n): n is MenuNode => n !== null))
    } else if (m.parentKeys && m.parentKeys.length > 0) {
      const g = ensureGroup(m.parentKeys[0], groups[m.parentKeys[0]])
      g.children!.push({ key: m.route, title: m.title, icon: m.icon })
    } else {
      roots.push({ key: m.route, title: m.title, icon: m.icon })
    }
  }

  return roots
}

/** 收集菜单树中所有叶子 key（路由路径），用于与动态菜单项去重 */
export function collectMenuKeys(nodes: MenuNode[]): Set<string> {
  const keys = new Set<string>()
  const walk = (list: MenuNode[]): void => {
    for (const n of list) {
      if (n.children && n.children.length > 0) walk(n.children)
      else keys.add(n.key)
    }
  }
  walk(nodes)
  return keys
}
