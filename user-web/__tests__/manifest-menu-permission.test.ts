import { it } from 'vitest'
import assert from 'node:assert/strict'
import { manifestToMenu } from '@shared/web/router/manifest.ts'
import type { MenuNode } from '@shared/web/router/manifest.ts'
import type { AppManifest } from '@shared/core/types/application.ts'

const manifests: AppManifest[] = [
  {
    id: 'org-users',
    route: '/org-users',
    name: 'OrgUsers',
    title: '组织用户',
    parentKeys: ['users'],
    requiredPermission: 'Base.Users',
  },
  {
    id: 'permissions',
    route: '/permissions',
    name: 'Permissions',
    title: '权限管理',
    parentKeys: ['users'],
    requiredPermission: 'Base.Roles',
  },
  {
    id: 'base-config',
    route: '/base-config',
    name: 'BaseConfig',
    title: '基础配置',
    parentKeys: ['base-config'],
    redirect: '/base-config/dict',
    children: [
      {
        id: 'dict',
        route: '/base-config/dict',
        name: 'Dict',
        title: '字典管理',
        parentKeys: ['base-config'],
        requiredPermission: 'Base.DictTypes',
      },
    ],
  },
  {
    id: 'dashboard',
    route: '/dashboard',
    name: 'Dashboard',
    title: '仪表盘',
  },
]

const groups = { users: { title: '用户权限' } }

/** 只比较 key 结构，避免依赖 icon 等对象形状细节 */
function shape(nodes: MenuNode[]): Array<{ key: string, children?: string[] }> {
  return nodes.map((n) => ({
    key: n.key,
    ...(n.children ? { children: n.children.map((c) => c.key) } : {}),
  }))
}

it('不传权限检查函数时返回完整菜单树（供角色权限配置页使用）', () => {
  assert.deepEqual(shape(manifestToMenu(manifests, groups)), [
    { key: 'users', children: ['/org-users', '/permissions'] },
    { key: 'base-config', children: ['/base-config/dict'] },
    { key: '/dashboard' },
  ])
})

it('仅授予 Base.Users 时过滤无权限菜单并剪除空分组', () => {
  const isGranted = (permission: string): boolean => permission === 'Base.Users'
  assert.deepEqual(shape(manifestToMenu(manifests, groups, isGranted)), [
    { key: 'users', children: ['/org-users'] },
    { key: '/dashboard' },
  ])
})
