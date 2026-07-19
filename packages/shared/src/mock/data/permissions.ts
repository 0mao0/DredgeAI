import type { PermissionItem } from '@shared/types'

export const mockPermissions: PermissionItem[] = [
  { id: '1', name: '仪表盘', code: 'dashboard:view', type: 'menu', status: '启用', sort: 1 },
  { id: '2', name: '用户管理', code: 'users:view', type: 'menu', status: '启用', sort: 2 },
  { id: '3', name: '创建用户', code: 'users:create', type: 'button', parentId: '2', status: '启用', sort: 1 },
  { id: '4', name: '编辑用户', code: 'users:edit', type: 'button', parentId: '2', status: '启用', sort: 2 },
  { id: '5', name: '删除用户', code: 'users:delete', type: 'button', parentId: '2', status: '禁用', sort: 3 },
  { id: '6', name: '应用管理', code: 'apps:view', type: 'menu', status: '启用', sort: 3 },
  { id: '7', name: '发布应用', code: 'apps:publish', type: 'button', parentId: '6', status: '启用', sort: 1 },
  { id: '8', name: '数据源', code: 'datasource:view', type: 'menu', status: '启用', sort: 4 },
  { id: '9', name: '系统日志', code: 'logs:view', type: 'menu', status: '启用', sort: 5 },
  { id: '10', name: '导出日志', code: 'logs:export', type: 'button', parentId: '9', status: '启用', sort: 1 },
  { id: '11', name: 'API 管理', code: 'api:view', type: 'api', status: '启用', sort: 6 },
  { id: '12', name: '用户数据查看', code: 'data:users:view', type: 'data', status: '启用', sort: 1 },
]
