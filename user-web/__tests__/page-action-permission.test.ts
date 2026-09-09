import { it } from 'vitest'
import assert from 'node:assert/strict'
import { manifestToRoutes } from '@shared/web/router/manifest.ts'
import { isActionGranted } from '@shared/web/composables/usePagePermissions.ts'
import type { AppManifest } from '@shared/core/types/application.ts'

it('manifestToRoutes 将 actionPermissions 透传至 route.meta（含嵌套 children）', () => {
  const manifests: AppManifest[] = [
    {
      id: 'permissions',
      route: '/permissions',
      name: 'Permissions',
      title: '权限管理',
      requiredPermission: 'Base.Roles',
      actionPermissions: { create: 'Base.Roles.Create', delete: 'Base.Roles.Delete' },
    },
    {
      id: 'users-group',
      route: '/users',
      name: 'UsersGroup',
      title: '用户权限',
      children: [
        {
          id: 'org-users',
          route: '/users/org-users',
          name: 'OrgUsers',
          title: '组织用户',
          actionPermissions: { assignRoles: 'Base.Users.ManageRoles' },
        },
      ],
    },
  ]

  const routes = manifestToRoutes(manifests)

  assert.deepEqual(routes[0].meta?.actionPermissions, {
    create: 'Base.Roles.Create',
    delete: 'Base.Roles.Delete',
  })
  assert.equal(routes[0].meta?.requiresPermission, 'Base.Roles')
  assert.deepEqual(routes[1].children?.[0].meta?.actionPermissions, {
    assignRoles: 'Base.Users.ManageRoles',
  })
})

it('isActionGranted：操作未在路由声明时放行', () => {
  assert.equal(isActionGranted({ create: 'Base.Roles.Create' }, 'export', () => false), true)
})

it('isActionGranted：已声明且已授权时放行', () => {
  assert.equal(
    isActionGranted({ create: 'Base.Roles.Create' }, 'create', (code) => code === 'Base.Roles.Create'),
    true,
  )
})

it('isActionGranted：已声明但未授权时拦截', () => {
  assert.equal(
    isActionGranted({ create: 'Base.Roles.Create' }, 'create', () => false),
    false,
  )
})
